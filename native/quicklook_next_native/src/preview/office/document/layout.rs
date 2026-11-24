//! Bounded document-order flow, rather than a gallery of package media.
//! Floating drawings flow at their anchor; this is not Word's pagination engine.

use std::io::{Read, Seek};

use quick_xml::events::Event;
use quick_xml::Reader;
use zip::ZipArchive;

use super::{docx_numbered_paragraph_prefix, docx_paragraph_prefix};
use crate::preview::office::layout::{
    image_item_from_relationship, parse_relationships, OfficeImagePlacement,
};
use crate::preview::{
    attr_value, local_xml_name, read_office_zip_text, xml_general_ref, xml_unescape_str,
    OfficeContext, OfficeLayoutDto, OfficeLayoutItemDto, OfficePageDto, OfficeResult,
    MAX_OFFICE_LAYOUT_IMAGES, OFFICE_EMUS_PER_DIP,
};

const MAX_OFFICE_DOCUMENT_PAGES: usize = 8;
const PAGE_WIDTH: f64 = 760.0;
const PAGE_HEIGHT: f64 = 980.0;
const MARGIN: f64 = 58.0;
const CONTENT_WIDTH: f64 = PAGE_WIDTH - MARGIN * 2.0;
const CONTENT_HEIGHT: f64 = PAGE_HEIGHT - MARGIN * 2.0;

pub(super) fn build_docx_layout<R: Read + Seek>(
    context: &mut OfficeContext,
    zip: &mut ZipArchive<R>,
    xml: &str,
) -> OfficeResult<Option<OfficeLayoutDto>> {
    context.check_cancelled()?;
    let rels = read_office_zip_text(
        context,
        zip,
        "word/_rels/document.xml.rels",
        2 * 1024 * 1024,
    )?
    .map(|xml| parse_relationships(context, &xml))
    .transpose()?
    .unwrap_or_default();
    let mut flow = DocumentFlow::new();
    let mut reader = Reader::from_str(xml);
    let mut in_text = false;
    let mut table_depth = 0usize;
    let mut paragraph_prefix = String::new();
    let mut paragraph_started = false;
    let mut drawing_depth = 0usize;
    let mut image_id = String::new();
    let mut image_size = (260.0, 170.0);
    // Bound failed lookups as well as successful images: each lookup scans ZIP metadata.
    let mut image_attempts = MAX_OFFICE_LAYOUT_IMAGES.min(6);
    let mut image_budget = image_attempts;
    let mut fallback_depth = 0usize;
    let mut event_count = 0;

    loop {
        let event = reader.read_event();
        event_count += 1;
        context.check_xml_event(event_count)?;
        match event {
            Ok(event @ (Event::Start(_) | Event::Empty(_))) => {
                let empty = matches!(event, Event::Empty(_));
                let (Event::Start(e) | Event::Empty(e)) = event else {
                    unreachable!();
                };
                let local = local_xml_name(e.name().as_ref());
                // DrawingML's VML fallback must not render a second copy of an image.
                if fallback_depth > 0 || local == "fallback" {
                    if !empty {
                        fallback_depth += 1;
                    }
                    continue;
                }
                match local.as_str() {
                    "drawing" | "pict" => {
                        if drawing_depth == 0 {
                            image_id.clear();
                            image_size = (260.0, 170.0);
                        }
                        if !empty {
                            drawing_depth += 1;
                        }
                    }
                    "blip" if drawing_depth > 0 && image_id.is_empty() => {
                        // Only embedded images; r:link never grants file/network authority.
                        image_id = attr_value(&e, "embed").unwrap_or_default();
                    }
                    "imagedata" if drawing_depth > 0 && image_id.is_empty() => {
                        image_id = attr_value(&e, "id").unwrap_or_default();
                    }
                    "extent" if drawing_depth > 0 => {
                        let dimension = |name| {
                            attr_value(&e, name)
                                .and_then(|value| value.parse::<u32>().ok())
                                .filter(|value| *value > 0)
                                .map(|value| f64::from(value) / OFFICE_EMUS_PER_DIP)
                        };
                        if let (Some(width), Some(height)) = (dimension("cx"), dimension("cy")) {
                            image_size = (width, height);
                        }
                    }
                    "t" if drawing_depth == 0 => {
                        in_text = !empty;
                        if !empty && !paragraph_started {
                            flow.append(&paragraph_prefix);
                            paragraph_started = true;
                        }
                    }
                    "tbl" if !empty => table_depth += 1,
                    "tr" if table_depth > 0 => flow.append("| "),
                    "pstyle" => paragraph_prefix = docx_paragraph_prefix(&e),
                    "numpr" => {
                        paragraph_prefix = docx_numbered_paragraph_prefix(&paragraph_prefix);
                    }
                    "tab" => flow.append(" "),
                    "br" if drawing_depth == 0 => {
                        flow.flush_text();
                        if attr_value(&e, "type").as_deref() == Some("page") {
                            flow.page_break();
                        }
                    }
                    _ => {}
                }
            }
            Ok(Event::End(e)) => {
                if fallback_depth > 0 {
                    fallback_depth -= 1;
                    continue;
                }
                let local = local_xml_name(e.name().as_ref());
                match local.as_str() {
                    "drawing" | "pict" if drawing_depth > 0 => {
                        drawing_depth -= 1;
                        if drawing_depth == 0 && !image_id.is_empty() && image_attempts > 0 {
                            image_attempts -= 1;
                            let scale = (CONTENT_WIDTH / image_size.0)
                                .min(CONTENT_HEIGHT / image_size.1)
                                .min(1.0);
                            if let Some(mut item) = image_item_from_relationship(
                                context,
                                zip,
                                "word/",
                                &rels,
                                OfficeImagePlacement {
                                    rel_id: &image_id,
                                    x: MARGIN,
                                    y: MARGIN,
                                    width: image_size.0 * scale,
                                    height: image_size.1 * scale,
                                    z_index: 0,
                                },
                                &mut image_budget,
                            )? {
                                flow.flush_text();
                                flow.make_room(item.height);
                                item.y = flow.y;
                                item.z_index = flow.items.len();
                                flow.y += item.height + 18.0;
                                flow.push(item);
                            }
                        }
                    }
                    "t" => in_text = false,
                    "p" if drawing_depth == 0 => {
                        if table_depth == 0 {
                            flow.flush_text();
                        } else {
                            flow.append(" ");
                        }
                        paragraph_prefix.clear();
                        paragraph_started = false;
                    }
                    "tc" if table_depth > 0 => flow.append(" | "),
                    "tr" if table_depth > 0 => flow.flush_text(),
                    "tbl" => table_depth = table_depth.saturating_sub(1),
                    _ => {}
                }
            }
            Ok(Event::Text(e)) if in_text => {
                flow.append(&xml_unescape_str(e.as_ref()));
            }
            Ok(Event::GeneralRef(e)) if in_text => flow.append(&xml_general_ref(e.as_ref())),
            Ok(Event::CData(e)) if in_text => flow.append(e.as_ref()),
            Ok(Event::Eof) => break,
            Err(_) => break,
            _ => {}
        }
    }
    context.check_cancelled()?;
    flow.flush_text();
    Ok(flow.finish())
}

struct DocumentFlow {
    pages: Vec<OfficePageDto>,
    items: Vec<OfficeLayoutItemDto>,
    page_index: usize,
    y: f64,
    text: String,
    text_chars: usize,
    has_content: bool,
}

impl DocumentFlow {
    fn new() -> Self {
        Self {
            pages: Vec::new(),
            items: Vec::new(),
            page_index: 1,
            y: MARGIN,
            text: String::new(),
            text_chars: 0,
            has_content: false,
        }
    }

    fn append(&mut self, value: &str) {
        // Preserve the existing per-paragraph clipping without retaining all XML text.
        for ch in value.chars().take(420 - self.text_chars) {
            self.text.push(ch);
            self.text_chars += 1;
        }
    }

    fn flush_text(&mut self) {
        let paragraph = std::mem::take(&mut self.text);
        self.text_chars = 0;
        let paragraph = paragraph.trim();
        if paragraph.is_empty() {
            return;
        }
        let line_count = (paragraph.chars().count() as f64 / 72.0)
            .ceil()
            .clamp(1.0, 5.0);
        let height = 24.0 * line_count + 10.0;
        self.make_room(height);
        self.push(OfficeLayoutItemDto {
            kind: "text".to_string(),
            x: MARGIN,
            y: self.y,
            width: CONTENT_WIDTH,
            height,
            z_index: self.items.len(),
            text: Some(paragraph.to_string()),
            shape: None,
            placeholder_type: None,
            bold: false,
            italic: false,
            font_size: None,
            fill_color: None,
            stroke_color: None,
            image_name: None,
            mime_type: None,
            image_ref: None,
            image_byte_length: None,
        });
        self.y += height + 6.0;
    }

    fn make_room(&mut self, height: f64) {
        if self.y + height > PAGE_HEIGHT - MARGIN && self.y > MARGIN {
            self.page_break();
        }
    }

    fn push(&mut self, item: OfficeLayoutItemDto) {
        self.has_content = true;
        if self.page_index <= MAX_OFFICE_DOCUMENT_PAGES {
            self.items.push(item);
        }
    }

    fn page_break(&mut self) {
        self.save_page();
        self.page_index += 1;
        self.y = MARGIN;
    }

    fn save_page(&mut self) {
        if self.page_index <= MAX_OFFICE_DOCUMENT_PAGES && !self.items.is_empty() {
            self.pages.push(OfficePageDto {
                title: format!("Page {}", self.page_index),
                index: self.page_index,
                width: PAGE_WIDTH,
                height: PAGE_HEIGHT,
                background_color: Some("#FFFFFF".to_string()),
                freeze_rows: None,
                freeze_columns: None,
                cells: Vec::new(),
                items: std::mem::take(&mut self.items),
            });
        }
    }

    fn finish(mut self) -> Option<OfficeLayoutDto> {
        if !self.has_content {
            return None;
        }
        self.save_page();
        Some(OfficeLayoutDto {
            layout_kind: "document".to_string(),
            width: PAGE_WIDTH,
            height: PAGE_HEIGHT,
            represented_page_count: self.pages.len(),
            total_page_count: self.page_index.max(self.pages.len()),
            page_limit: MAX_OFFICE_DOCUMENT_PAGES,
            pages: self.pages,
        })
    }
}
