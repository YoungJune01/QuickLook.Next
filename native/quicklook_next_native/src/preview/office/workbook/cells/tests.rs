use super::super::{parse_xlsx_merge_regions, OfficeContext};
use super::{
    parse_worksheet_layout_cells, BTreeMap, OfficeCellDto, XlsxSheetMetrics, XlsxStyle,
    MAX_OFFICE_ROWS,
};

fn parse(xml: &str, styles: &[XlsxStyle]) -> Vec<OfficeCellDto> {
    let context = OfficeContext::new(None);
    let merges = parse_xlsx_merge_regions(&context, xml).unwrap();
    parse_worksheet_layout_cells(
        &context,
        xml,
        &[],
        &XlsxSheetMetrics::default(),
        &merges,
        styles,
    )
    .unwrap()
}

#[test]
fn blank_self_closing_and_paired_cells_keep_style_and_geometry() {
    let styles = [
        XlsxStyle::default(),
        XlsxStyle {
            fill_color: Some("#FFE699".into()),
            bold: true,
            horizontal_alignment: Some("center".into()),
            ..Default::default()
        },
    ];
    let cells = parse(
        r#"<worksheet><sheetData><row r="4">
        <c r="G4" s="1"/><c r="H4" s="1"></c><c r="I4" s="1"><v/></c>
        </row></sheetData></worksheet>"#,
        &styles,
    );
    assert_eq!(cells.len(), 3);
    for (index, cell) in cells.iter().enumerate() {
        assert_eq!((cell.row, cell.column), (3, index + 6));
        assert_eq!(cell.text, "");
        assert_eq!(cell.fill_color.as_deref(), Some("#FFE699"));
        assert_eq!(cell.horizontal_alignment.as_deref(), Some("center"));
        assert!(cell.bold);
        assert_eq!(cell.x, (index + 6) as f64 * 96.0);
        assert_eq!((cell.y, cell.width, cell.height), (84.0, 96.0, 28.0));
    }
}

#[test]
fn blank_cells_advance_implicit_column_positions_without_leaking_text() {
    let cells = parse(
        r#"<worksheet><sheetData><row r="1">
        <c t="inlineStr"><is><t>First &amp; value</t></is></c><c/>
        <c><v>42</v></c><c><v>  </v></c>
        </row></sheetData></worksheet>"#,
        &[],
    );
    assert_eq!(cells.len(), 4);
    assert_eq!(
        cells.iter().map(|c| c.column).collect::<Vec<_>>(),
        [0, 1, 2, 3]
    );
    assert_eq!(
        cells.iter().map(|c| c.text.as_str()).collect::<Vec<_>>(),
        ["First & value", "", "42", ""]
    );
}

#[test]
fn empty_merged_ranges_keep_one_anchor_and_suppress_interior_placeholders() {
    let cells = parse(
        r#"<worksheet><sheetData>
        <row r="4"><c r="G4"/><c r="H4"/><c r="I4"/></row>
        <row r="5"><c r="H5"/><c r="I5"/></row>
        <row r="6"><c r="H6"/><c r="I6"/></row>
        </sheetData><mergeCells><mergeCell ref="H4:I6"/>
        <mergeCell ref="G7:H8"/></mergeCells></worksheet>"#,
        &[],
    );
    assert_eq!(cells.len(), 3);
    let explicit = cells.iter().find(|c| (c.row, c.column) == (3, 7)).unwrap();
    assert_eq!((explicit.row_span, explicit.column_span), (3, 2));
    assert_eq!((explicit.width, explicit.height), (192.0, 84.0));
    let omitted = cells.iter().find(|c| (c.row, c.column) == (6, 6)).unwrap();
    assert_eq!((omitted.row_span, omitted.column_span), (2, 2));
    assert_eq!(omitted.text, "");
    assert!(!cells.iter().any(|c| (c.row, c.column) == (4, 8)));
}

#[test]
fn empty_dimensions_do_not_expand_into_an_unbounded_grid() {
    assert!(parse(
        r#"<worksheet><dimension ref="A1:XFD1048576"/><sheetData/></worksheet>"#,
        &[]
    )
    .is_empty());
}

#[test]
fn blank_and_duplicate_records_stay_inside_the_existing_cell_budget() {
    let mut xml = String::from("<worksheet><sheetData>");
    for row in 1..=55 {
        xml.push_str(&format!("<row r=\"{row}\">"));
        for _ in 0..36 {
            xml.push_str("<c/>");
        }
        xml.push_str("</row>");
    }
    for _ in 0..1_000 {
        xml.push_str("<row r=\"1\"><c r=\"A1\"/></row>");
    }
    xml.push_str("</sheetData></worksheet>");
    let cells = parse(&xml, &[]);
    assert_eq!(cells.len(), MAX_OFFICE_ROWS * 32);
    assert!(cells
        .iter()
        .all(|c| c.row < MAX_OFFICE_ROWS && c.column < 32));
}

#[test]
fn oversized_merge_spans_are_clipped_to_the_preview_window() {
    let cells = parse(
        r#"<worksheet><mergeCells><mergeCell ref="A1:XFD1048576"/></mergeCells></worksheet>"#,
        &[],
    );
    assert_eq!(cells.len(), 1);
    assert_eq!(
        (cells[0].row_span, cells[0].column_span),
        (MAX_OFFICE_ROWS, 32)
    );
    assert_eq!(
        (cells[0].width, cells[0].height),
        (32.0 * 96.0, 48.0 * 28.0)
    );
}

#[test]
fn overflowing_references_do_not_panic_or_create_unbounded_cells() {
    let letters = "Z".repeat(80);
    let digits = "9".repeat(80);
    let xml = format!(
        r#"<worksheet><sheetData><row r="1"><c r="{letters}1"/>
        <c r="A1"/></row><row r="18446744073709551615"><c r="A1"/></row>
        </sheetData><mergeCells><mergeCell ref="A1:{letters}{digits}"/>
        <mergeCell ref="A1:A{digits}"/></mergeCells></worksheet>"#
    );
    let cells = parse(&xml, &[]);
    assert_eq!(cells.len(), 1);
    assert_eq!((cells[0].row, cells[0].column), (0, 0));
}

#[test]
fn blank_cell_parsing_honors_pre_cancellation() {
    extern "C" fn cancelled() -> bool {
        true
    }
    let result = parse_worksheet_layout_cells(
        &OfficeContext::new(Some(cancelled)),
        "<worksheet><sheetData><row><c/></row></sheetData></worksheet>",
        &[],
        &XlsxSheetMetrics::default(),
        &BTreeMap::new(),
        &[],
    );
    assert!(matches!(
        result,
        Err(super::super::super::super::OfficeReadError::Cancelled)
    ));
}
