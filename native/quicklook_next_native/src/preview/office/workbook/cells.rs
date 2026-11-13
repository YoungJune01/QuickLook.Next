use std::collections::BTreeMap;

use quick_xml::{
    events::{BytesStart, Event},
    Reader,
};

use super::{
    attr_value, cell_reference_column, clean_table_cell, is_inside_non_origin_merge,
    local_xml_name, resolve_cell_value, xlsx_col_span_width, xlsx_col_x, xlsx_row_span_height,
    xlsx_row_y, xml_general_ref, xml_unescape_bytes, OfficeCellDto, OfficeContext, OfficeResult,
    XlsxMergeRegion, XlsxSheetMetrics, XlsxStyle, MAX_OFFICE_ROWS,
};

#[cfg(test)]
mod tests;

struct CellInput {
    column: usize,
    style: usize,
    kind: String,
    value: String,
}

impl CellInput {
    fn from_element(element: &BytesStart, next_column: &mut usize) -> Option<Self> {
        let column = match attr_value(element, "r") {
            Some(reference) => cell_reference_column(&reference)?,
            None => *next_column,
        };
        *next_column = column.saturating_add(1);
        (column < 32).then(|| Self {
            column,
            style: attr_value(element, "s")
                .and_then(|v| v.parse().ok())
                .unwrap_or(0),
            kind: attr_value(element, "t").unwrap_or_default(),
            value: String::new(),
        })
    }
}

pub(super) fn parse_worksheet_layout_cells(
    context: &OfficeContext,
    xml: &str,
    shared_strings: &[String],
    metrics: &XlsxSheetMetrics,
    merge_regions: &BTreeMap<(usize, usize), XlsxMergeRegion>,
    styles: &[XlsxStyle],
) -> OfficeResult<Vec<OfficeCellDto>> {
    context.check_cancelled()?;
    let mut reader = Reader::from_str(xml);
    // One retained cell per coordinate, including blank cells. Duplicate XML
    // records cannot grow retention beyond the existing 48-row/32-column window.
    let mut cells = BTreeMap::new();
    let mut in_row = false;
    let mut in_value = false;
    let mut in_inline_text = false;
    let mut display_row = 0usize;
    let mut row_index = 0usize;
    let mut next_column = 0usize;
    let mut current: Option<CellInput> = None;
    let mut event_count = 0;

    loop {
        let event = reader.read_event();
        event_count += 1;
        context.check_xml_event(event_count)?;
        match event {
            Ok(Event::Start(e)) => match local_xml_name(e.name().as_ref()).as_str() {
                "row" => {
                    in_row = true;
                    display_row = display_row.saturating_add(1);
                    row_index = attr_value(&e, "r")
                        .and_then(|v| v.parse::<usize>().ok())
                        .unwrap_or(display_row)
                        .saturating_sub(1);
                    next_column = 0;
                }
                "c" if in_row => {
                    in_value = false;
                    in_inline_text = false;
                    current = if row_index < MAX_OFFICE_ROWS {
                        CellInput::from_element(&e, &mut next_column)
                    } else {
                        None
                    };
                }
                "v" if current.is_some() => in_value = true,
                "t" if current.is_some() => in_inline_text = true,
                _ => {}
            },
            Ok(Event::Empty(e)) if in_row && local_xml_name(e.name().as_ref()) == "c" => {
                if row_index < MAX_OFFICE_ROWS {
                    if let Some(input) = CellInput::from_element(&e, &mut next_column) {
                        retain_cell(
                            &mut cells,
                            row_index,
                            input,
                            shared_strings,
                            metrics,
                            merge_regions,
                            styles,
                        );
                    }
                }
            }
            Ok(Event::End(e)) => match local_xml_name(e.name().as_ref()).as_str() {
                "v" => in_value = false,
                "t" => in_inline_text = false,
                "c" => {
                    if let Some(input) = current.take() {
                        retain_cell(
                            &mut cells,
                            row_index,
                            input,
                            shared_strings,
                            metrics,
                            merge_regions,
                            styles,
                        );
                    }
                    in_value = false;
                    in_inline_text = false;
                }
                "row" => {
                    in_row = false;
                    current = None;
                }
                _ => {}
            },
            Ok(Event::Text(e)) if in_value || in_inline_text => {
                if let Some(input) = current.as_mut() {
                    input.value.push_str(&xml_unescape_bytes(e.as_ref()));
                }
            }
            Ok(Event::GeneralRef(e)) if in_value || in_inline_text => {
                if let Some(input) = current.as_mut() {
                    input.value.push_str(&xml_general_ref(e.as_ref()));
                }
            }
            Ok(Event::CData(e)) if in_value || in_inline_text => {
                if let Some(input) = current.as_mut() {
                    input.value.push_str(&String::from_utf8_lossy(e.as_ref()));
                }
            }
            Ok(Event::Eof) => break,
            Err(_) => break,
            _ => {}
        }
    }

    // Some producers omit the anchor <c> entirely for an empty merged range.
    // Preserve that range once, without painting its interior cells over it.
    for region in merge_regions.values() {
        context.check_cancelled()?;
        if !cells.contains_key(&(region.first_row, region.first_col)) {
            retain_cell(
                &mut cells,
                region.first_row,
                CellInput {
                    column: region.first_col,
                    style: 0,
                    kind: String::new(),
                    value: String::new(),
                },
                shared_strings,
                metrics,
                merge_regions,
                styles,
            );
        }
    }
    Ok(cells.into_values().collect())
}

fn retain_cell(
    cells: &mut BTreeMap<(usize, usize), OfficeCellDto>,
    row: usize,
    input: CellInput,
    shared_strings: &[String],
    metrics: &XlsxSheetMetrics,
    merge_regions: &BTreeMap<(usize, usize), XlsxMergeRegion>,
    styles: &[XlsxStyle],
) {
    let column = input.column;
    if row >= MAX_OFFICE_ROWS
        || column >= 32
        || is_inside_non_origin_merge(merge_regions, row, column)
    {
        return;
    }
    let merge = merge_regions.get(&(row, column));
    let row_span = merge
        .map(|m| m.row_span)
        .unwrap_or(1)
        .min(MAX_OFFICE_ROWS - row);
    let column_span = merge.map(|m| m.column_span).unwrap_or(1).min(32 - column);
    let style = styles.get(input.style).cloned().unwrap_or_default();
    cells.insert(
        (row, column),
        OfficeCellDto {
            row,
            column,
            text: clean_table_cell(&resolve_cell_value(
                &input.value,
                &input.kind,
                shared_strings,
            )),
            x: xlsx_col_x(metrics, column),
            y: xlsx_row_y(metrics, row),
            width: xlsx_col_span_width(metrics, column, column + column_span),
            height: xlsx_row_span_height(metrics, row, row + row_span),
            row_span,
            column_span,
            number_format: style.number_format,
            fill_color: style.fill_color,
            text_color: style.text_color,
            horizontal_alignment: style.horizontal_alignment,
            vertical_alignment: style.vertical_alignment,
            bold: style.bold,
            italic: style.italic,
            font_size: style.font_size,
            wrap_text: style.wrap_text,
        },
    );
}
