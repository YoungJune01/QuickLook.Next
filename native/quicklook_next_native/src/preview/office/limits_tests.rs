use std::io::{Cursor, Write};

use zip::{write::SimpleFileOptions, CompressionMethod, ZipArchive, ZipWriter};

use super::super::{
    read_office_limited_to_end, read_office_zip_bytes, read_office_zip_text, render_office_reader,
    OfficeContext, OfficeReadError, ReaderPreviewError, MAX_OFFICE_DECOMPRESSED_BYTES,
};

fn archive(entries: &[(&str, &[u8])]) -> Vec<u8> {
    let mut writer = ZipWriter::new(Cursor::new(Vec::new()));
    for (name, bytes) in entries {
        writer
            .start_file(
                *name,
                SimpleFileOptions::default().compression_method(CompressionMethod::Deflated),
            )
            .unwrap();
        writer.write_all(bytes).unwrap();
    }
    writer.finish().unwrap().into_inner()
}

fn render(bytes: Vec<u8>) -> Result<String, ReaderPreviewError> {
    let length = bytes.len() as u64;
    render_office_reader(Cursor::new(bytes), "sample.xlsx", length, 0, None)
}

#[test]
fn oversized_xml_is_a_limit_error_including_normalized_entry_names() {
    for name in ["xl/worksheets/sheet1.xml", "XL\\WORKSHEETS\\SHEET1.XML"] {
        let bytes = archive(&[(name, b"123456789")]);
        let mut zip = ZipArchive::new(Cursor::new(bytes)).unwrap();
        let mut context = OfficeContext::new(None);
        assert!(matches!(
            read_office_zip_text(&mut context, &mut zip, "xl/worksheets/sheet1.xml", 8),
            Err(OfficeReadError::BudgetExhausted)
        ));
        // Reject from archive metadata before decompressing or allocating the part.
        assert_eq!(
            context.remaining_decompressed_bytes,
            MAX_OFFICE_DECOMPRESSED_BYTES
        );
    }
}

#[test]
fn exact_xml_limit_and_missing_parts_remain_distinct_from_oversize() {
    let bytes = archive(&[("xl/worksheets/sheet1.xml", b"12345678")]);
    let mut zip = ZipArchive::new(Cursor::new(bytes)).unwrap();
    let mut context = OfficeContext::new(None);
    assert_eq!(
        read_office_zip_text(&mut context, &mut zip, "xl/worksheets/sheet1.xml", 8).unwrap(),
        Some("12345678".into())
    );
    assert_eq!(
        read_office_zip_text(&mut context, &mut zip, "xl/sharedStrings.xml", 8).unwrap(),
        None
    );
}

#[test]
fn optional_oversized_media_can_still_be_skipped() {
    let bytes = archive(&[("xl/media/image1.png", b"123456789")]);
    let mut zip = ZipArchive::new(Cursor::new(bytes)).unwrap();
    assert!(read_office_zip_bytes(
        &mut OfficeContext::new(None),
        &mut zip,
        "xl/media/image1.png",
        8
    )
    .unwrap()
    .is_none());
}

#[test]
fn actual_stream_overflow_and_cancellation_are_typed_failures() {
    let mut reader = Cursor::new(b"123456789");
    assert!(matches!(
        read_office_limited_to_end(&mut OfficeContext::new(None), &mut reader, 8),
        Err(OfficeReadError::BudgetExhausted)
    ));
    extern "C" fn cancelled() -> bool {
        true
    }
    reader.set_position(0);
    assert!(matches!(
        read_office_limited_to_end(&mut OfficeContext::new(Some(cancelled)), &mut reader, 8),
        Err(OfficeReadError::Cancelled)
    ));
}

#[test]
fn oversized_workbook_parts_propagate_limit_instead_of_empty_cells() {
    // Real entry caps: the compressed XLSX is tiny while its XML exceeds the cap.
    for (part, limit) in [
        ("xl/worksheets/sheet1.xml", 16 * 1024 * 1024),
        ("xl/sharedStrings.xml", 16 * 1024 * 1024),
        ("xl/styles.xml", 4 * 1024 * 1024),
        ("xl/workbook.xml", 2 * 1024 * 1024),
    ] {
        let oversized = vec![b' '; limit + 1];
        let bytes = archive(&[(part, &oversized)]);
        assert!(bytes.len() < 64 * 1024);
        assert!(
            matches!(render(bytes), Err(ReaderPreviewError::LimitExceeded)),
            "{part}"
        );
    }
}

#[test]
fn fifty_thousand_rows_within_byte_budget_keep_a_bounded_preview() {
    let mut xml = String::from("<worksheet><sheetData>");
    for row in 1..=50_000 {
        xml.push_str(&format!(
            "<row r=\"{row}\"><c r=\"A{row}\" t=\"inlineStr\"><is><t>row-{row}</t></is></c></row>"
        ));
    }
    xml.push_str("</sheetData></worksheet>");
    let json = render(archive(&[("xl/worksheets/sheet1.xml", xml.as_bytes())])).unwrap();
    let model: serde_json::Value = serde_json::from_str(&json).unwrap();
    let cells = model["officeLayout"]["pages"][0]["cells"]
        .as_array()
        .unwrap();
    assert_eq!(cells.len(), 48);
    assert_eq!(cells[0]["text"], "row-1");
    assert_eq!(cells[47]["text"], "row-48");
    assert!(!json.contains("no extractable worksheet cells"));
}

#[test]
fn genuinely_empty_worksheet_is_not_reported_as_too_large() {
    let json = render(archive(&[(
        "xl/worksheets/sheet1.xml",
        b"<worksheet><sheetData/></worksheet>",
    )]))
    .unwrap();
    let model: serde_json::Value = serde_json::from_str(&json).unwrap();
    assert!(model["officeLayout"]["pages"][0]["cells"]
        .as_array()
        .unwrap()
        .is_empty());
}
