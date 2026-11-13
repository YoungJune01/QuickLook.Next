use super::{
    attr_value, count_numbered_parts, local_xml_name, normalize_zip_target, parse_relationships,
    read_office_zip_text, OfficeContext, OfficeResult, MAX_OFFICE_SHEETS,
};
use quick_xml::events::Event;
use quick_xml::Reader;
use std::io::{Read, Seek};
use zip::ZipArchive;

#[cfg(test)]
mod tests;

pub(super) struct XlsxSheet {
    pub(super) title: String,
    pub(super) part: String,
}

pub(super) fn workbook_sheets<R: Read + Seek>(
    context: &mut OfficeContext,
    zip: &mut ZipArchive<R>,
) -> OfficeResult<(Vec<XlsxSheet>, usize)> {
    let workbook = read_office_zip_text(context, zip, "xl/workbook.xml", 2 * 1024 * 1024)?;
    let relationships =
        read_office_zip_text(context, zip, "xl/_rels/workbook.xml.rels", 2 * 1024 * 1024)?;
    let (Some(workbook), Some(relationships)) = (workbook, relationships) else {
        // Compatibility for old minimal producer output without workbook metadata.
        let total = count_numbered_parts(context, zip, "xl/worksheets/", "sheet")?;
        return Ok((
            (1..=MAX_OFFICE_SHEETS)
                .map(|index| XlsxSheet {
                    title: format!("Sheet {index}"),
                    part: format!("xl/worksheets/sheet{index}.xml"),
                })
                .collect(),
            total,
        ));
    };
    let relationships = parse_relationships(context, &relationships)?;
    let mut reader = Reader::from_str(&workbook);
    let mut sheets = Vec::new();
    let mut total = 0usize;
    let mut event_count = 0;
    loop {
        let event = reader.read_event();
        event_count += 1;
        context.check_xml_event(event_count)?;
        match event {
            Ok(Event::Start(e)) | Ok(Event::Empty(e))
                if local_xml_name(e.name().as_ref()) == "sheet" =>
            {
                total = total.saturating_add(1);
                if total > MAX_OFFICE_SHEETS {
                    continue;
                }
                let Some(target) = attr_value(&e, "id").and_then(|id| relationships.get(&id))
                else {
                    continue;
                };
                let part = normalize_zip_target("xl/", target);
                // Workbook relationships are untrusted archive metadata, never file
                // paths or authority to fetch an external workbook/worksheet.
                if !part.starts_with("xl/worksheets/")
                    || !part.ends_with(".xml")
                    || part.contains(':')
                {
                    continue;
                }
                let title = attr_value(&e, "name")
                    .filter(|name| !name.trim().is_empty())
                    .map(|name| name.chars().take(128).collect())
                    .unwrap_or_else(|| format!("Sheet {total}"));
                sheets.push(XlsxSheet { title, part });
            }
            Ok(Event::Eof) => break,
            Err(_) => return Ok((Vec::new(), total)),
            _ => {}
        }
    }
    Ok((sheets, total))
}
