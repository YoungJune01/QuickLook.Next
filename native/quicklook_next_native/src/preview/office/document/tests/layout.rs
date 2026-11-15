use super::{build_docx_layout, test_office_context, OfficeContext, OfficeReadError};
use std::io::{Cursor, Write};
use zip::ZipArchive;

fn layout_fixture(xml: &str, rels: &str) -> crate::preview::OfficeLayoutDto {
    let mut writer = zip::ZipWriter::new(Cursor::new(Vec::new()));
    for (name, bytes) in [
        ("word/_rels/document.xml.rels", rels.as_bytes()),
        ("word/media/image1.png", b"one".as_slice()),
        ("word/media/image2.png", b"two".as_slice()),
        ("word/media/unused.png", b"unused".as_slice()),
    ] {
        writer
            .start_file(name, zip::write::SimpleFileOptions::default())
            .unwrap();
        writer.write_all(bytes).unwrap();
    }
    let mut zip = ZipArchive::new(writer.finish().unwrap()).unwrap();
    build_docx_layout(&mut test_office_context(), &mut zip, xml)
        .unwrap()
        .unwrap()
}

const IMAGE_RELS: &str = r#"<Relationships>
    <Relationship Id="first" Target="media/image2.png"/>
    <Relationship Id="second" Target="media/image1.png"/>
    <Relationship Id="external" Target="media/unused.png" TargetMode="External"/>
    <Relationship Id="traversal" Target="../../outside.png"/>
</Relationships>"#;

#[test]
fn docx_layout_preserves_text_image_order_and_repeated_placements() {
    let layout = layout_fixture(
        r#"<w:body>
        <w:p><w:r><w:t>Before</w:t><w:drawing><wp:inline>
            <a:blip r:embed="first"/><wp:extent cx="1905000" cy="952500"/>
        </wp:inline></w:drawing><w:t>Between</w:t></w:r></w:p>
        <w:p><w:r><w:drawing><wp:anchor><wp:extent cx="952500" cy="1905000"/>
            <a:blip r:embed="second"/></wp:anchor></w:drawing><w:t>After</w:t></w:r></w:p>
        <w:p><w:r><w:drawing><a:blip r:embed="first"/></w:drawing></w:r></w:p>
        <w:sectPr/>
    </w:body>"#,
        IMAGE_RELS,
    );
    let items: Vec<_> = layout.pages.iter().flat_map(|page| &page.items).collect();
    let order: Vec<_> = items
        .iter()
        .map(|item| item.text.as_deref().or(item.image_ref.as_deref()).unwrap())
        .collect();
    assert_eq!(
        order,
        [
            "Before",
            "word/media/image2.png",
            "Between",
            "word/media/image1.png",
            "After",
            "word/media/image2.png"
        ]
    );
    assert_eq!((items[1].width, items[1].height), (200.0, 100.0));
    assert_eq!((items[3].width, items[3].height), (100.0, 200.0));
    for pair in items.windows(2) {
        assert!(pair[0].y + pair[0].height <= pair[1].y);
    }
}

#[test]
fn docx_layout_ignores_unreferenced_missing_external_and_unsafe_images() {
    let layout = layout_fixture(
        r#"<w:body><w:p><w:r><w:t>Before</w:t>
        <w:drawing><a:blip r:embed="missing"/></w:drawing>
        <w:drawing><a:blip r:embed="external"/></w:drawing>
        <w:drawing><a:blip r:embed="traversal"/></w:drawing>
        <w:drawing><a:blip r:link="first"/></w:drawing>
        <w:t>After</w:t></w:r></w:p></w:body>"#,
        IMAGE_RELS,
    );
    assert_eq!(layout.pages[0].items.len(), 1);
    assert_eq!(
        layout.pages[0].items[0].text.as_deref(),
        Some("BeforeAfter")
    );
}

#[test]
fn docx_layout_handles_table_images_vml_and_alternate_content_once() {
    let layout = layout_fixture(
        r#"<w:body><w:tbl><w:tr><w:tc><w:p><w:r>
        <w:t>Cell before</w:t><mc:AlternateContent><mc:Choice>
        <w:drawing><a:blip r:embed="first"/></w:drawing>
        </mc:Choice><mc:Fallback><w:pict><v:imagedata r:id="first"/></w:pict>
        </mc:Fallback></mc:AlternateContent><w:t>Cell after</w:t>
        </w:r></w:p></w:tc></w:tr></w:tbl>
        <w:p><w:r><w:pict><v:imagedata r:id="second"/></w:pict></w:r></w:p>
    </w:body>"#,
        IMAGE_RELS,
    );
    let items = &layout.pages[0].items;
    assert_eq!(items.len(), 4);
    assert!(items[0].text.as_deref().unwrap().contains("Cell before"));
    assert_eq!(items[1].image_ref.as_deref(), Some("word/media/image2.png"));
    assert!(items[2].text.as_deref().unwrap().contains("Cell after"));
    assert_eq!(items[3].image_ref.as_deref(), Some("word/media/image1.png"));
}

#[test]
fn docx_layout_caps_images_and_failed_lookups_and_clips_paragraphs() {
    let drawing = "<w:drawing><a:blip r:embed=\"first\"/></w:drawing>";
    let layout = layout_fixture(
        &format!(
            "<w:p><w:r><w:t>{}</w:t>{}</w:r></w:p>",
            "x".repeat(10_000),
            drawing.repeat(20)
        ),
        IMAGE_RELS,
    );
    let items: Vec<_> = layout.pages.iter().flat_map(|page| &page.items).collect();
    assert_eq!(items[0].text.as_deref().unwrap().chars().count(), 420);
    assert_eq!(items.iter().filter(|item| item.kind == "image").count(), 6);
    let layout = layout_fixture(
        &format!(
            "<w:p><w:r><w:t>Text</w:t>{}{drawing}</w:r></w:p>",
            drawing.replace("first", "missing").repeat(20)
        ),
        IMAGE_RELS,
    );
    assert_eq!(layout.pages[0].items.len(), 1);
}

#[test]
fn docx_layout_fits_large_images_and_honors_explicit_page_breaks() {
    let layout = layout_fixture(
        r#"<w:body><w:p><w:r><w:t>First page</w:t>
        <w:br w:type="page"/><w:drawing><wp:extent cx="4294967295" cy="2147483647"/>
        <a:blip r:embed="first"/></w:drawing><w:t>After image</w:t></w:r></w:p></w:body>"#,
        IMAGE_RELS,
    );
    assert_eq!(layout.pages.len(), 2);
    assert_eq!(layout.pages[0].items.len(), 1);
    let image = &layout.pages[1].items[0];
    assert_eq!(image.kind, "image");
    assert!(image.width <= 644.0 && image.height <= 864.0);
    assert!((image.width / image.height - 2.0).abs() < 0.00001);
}

#[test]
fn docx_layout_cancellation_during_xml_and_malformed_dimensions_are_safe() {
    use std::cell::Cell;
    thread_local! { static POLLS: Cell<usize> = const { Cell::new(0) }; }
    extern "C" fn cancel_during_xml() -> bool {
        POLLS.with(|polls| {
            polls.set(polls.get() + 1);
            polls.get() >= 4
        })
    }
    let mut zip = ZipArchive::new(
        zip::ZipWriter::new(Cursor::new(Vec::new()))
            .finish()
            .unwrap(),
    )
    .unwrap();
    let xml = format!(
        "<w:body>{}</w:body>",
        "<w:p><w:r><w:t>x</w:t></w:r></w:p>".repeat(4096)
    );
    assert!(matches!(
        build_docx_layout(
            &mut OfficeContext::new(Some(cancel_during_xml)),
            &mut zip,
            &xml
        ),
        Err(OfficeReadError::Cancelled)
    ));
    let layout = layout_fixture(
        r#"<w:p><w:r><w:t>Text</w:t><w:drawing>
        <wp:extent cx="NaN" cy="-2"/><a:blip r:embed="first"/>
        </w:drawing><w:t>Trailing"#,
        IMAGE_RELS,
    );
    assert_eq!(layout.pages[0].items[1].width, 260.0);
    assert_eq!(layout.pages[0].items[1].height, 170.0);
}
