use super::super::{build_xlsx_layout, OfficeContext};
use std::io::{Cursor, Write};
use zip::ZipArchive;

fn test_office_context() -> OfficeContext {
    OfficeContext::new(None)
}

fn test_zip_bytes(entries: &[(&str, &[u8])]) -> Vec<u8> {
    let mut writer = zip::ZipWriter::new(Cursor::new(Vec::new()));
    for (name, bytes) in entries {
        writer
            .start_file(*name, zip::write::SimpleFileOptions::default())
            .expect("start ZIP entry");
        writer.write_all(bytes).expect("write ZIP entry");
    }
    writer.finish().expect("finish ZIP").into_inner()
}

#[test]
fn xlsx_tabs_follow_workbook_names_relationships_and_order_including_empty_sheets() {
    let bytes = test_zip_bytes(&[
        ("xl/workbook.xml", br#"<workbook xmlns:r="r"><sheets><sheet name="Notes &amp; sources" r:id="r2"/><sheet name="Data" r:id="r1"/></sheets></workbook>"#),
        ("xl/_rels/workbook.xml.rels", br#"<Relationships><Relationship Id="r1" Target="worksheets/sheet8.xml"/><Relationship Id="r2" Target="/xl/worksheets/sheet2.xml"/></Relationships>"#),
        ("xl/worksheets/sheet2.xml", b"<worksheet><sheetData/></worksheet>"),
        ("xl/worksheets/sheet8.xml", br#"<worksheet><sheetData><row r="1" ht="15"><c r="A1" t="inlineStr"><is><t>Visible data</t></is></c></row></sheetData></worksheet>"#),
    ]);
    let mut zip = ZipArchive::new(Cursor::new(bytes)).unwrap();
    let layout = build_xlsx_layout(&mut test_office_context(), &mut zip, &[])
        .unwrap()
        .unwrap();
    assert_eq!(layout.total_page_count, 2);
    assert_eq!(layout.pages.len(), 2);
    assert_eq!(layout.pages[0].title, "Notes & sources");
    assert!(layout.pages[0].cells.is_empty());
    assert_eq!(layout.pages[1].title, "Data");
    assert_eq!(layout.pages[1].cells[0].text, "Visible data");
    assert_eq!(layout.pages[1].cells[0].height, 20.0);
}

#[test]
fn xlsx_workbook_relationships_cannot_substitute_external_or_other_parts() {
    let bytes = test_zip_bytes(&[
        ("xl/workbook.xml", br#"<workbook xmlns:r="r"><sheets><sheet name="External" r:id="r1"/><sheet name="Other" r:id="r2"/><sheet name="Missing" r:id="absent"/></sheets></workbook>"#),
        ("xl/_rels/workbook.xml.rels", br#"<Relationships><Relationship Id="r1" Target="https://example.invalid/x.xml" TargetMode="External"/><Relationship Id="r2" Target="worksheets/../media/fake.xml"/></Relationships>"#),
        ("xl/worksheets/sheet1.xml", br#"<worksheet><sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>Must not replace another sheet</t></is></c></row></sheetData></worksheet>"#),
    ]);
    let mut zip = ZipArchive::new(Cursor::new(bytes)).unwrap();
    assert!(build_xlsx_layout(&mut test_office_context(), &mut zip, &[])
        .unwrap()
        .is_none());
}

#[test]
fn xlsx_workbook_metadata_keeps_the_six_sheet_limit_and_bounds_titles() {
    let name = "界".repeat(200);
    let workbook = format!(
        "<workbook xmlns:r=\"r\"><sheets>{}</sheets></workbook>",
        (0..7)
            .map(|_| format!("<sheet name=\"{name}\" r:id=\"r1\"/>"))
            .collect::<String>()
    );
    let bytes = test_zip_bytes(&[
        ("xl/workbook.xml", workbook.as_bytes()),
        ("xl/_rels/workbook.xml.rels", br#"<Relationships><Relationship Id="r1" Target="worksheets/sheet1.xml"/></Relationships>"#),
        ("xl/worksheets/sheet1.xml", b"<worksheet><sheetData/></worksheet>"),
    ]);
    let mut zip = ZipArchive::new(Cursor::new(bytes)).unwrap();
    let layout = build_xlsx_layout(&mut test_office_context(), &mut zip, &[])
        .unwrap()
        .unwrap();
    assert_eq!(layout.total_page_count, 7);
    assert_eq!(layout.pages.len(), 6);
    assert_eq!(layout.page_limit, 6);
    assert!(layout
        .pages
        .iter()
        .all(|page| page.title.chars().count() == 128));
}

#[test]
fn malformed_workbook_metadata_does_not_choose_a_numbered_fallback() {
    let bytes = test_zip_bytes(&[
        ("xl/workbook.xml", b"<workbook><sheets></workbook>"),
        ("xl/_rels/workbook.xml.rels", b"<Relationships/>"),
        (
            "xl/worksheets/sheet1.xml",
            b"<worksheet><sheetData/></worksheet>",
        ),
    ]);
    let mut zip = ZipArchive::new(Cursor::new(bytes)).unwrap();
    assert!(build_xlsx_layout(&mut test_office_context(), &mut zip, &[])
        .unwrap()
        .is_none());
}
