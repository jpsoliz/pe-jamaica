from __future__ import annotations

import html
import re
import sys
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "parcel-workflow-extension-user-training-and-test-guide.md"
OUTPUT = ROOT / "Sidwell_Cadastre_Tools_User_Training_and_Test_Guide.docx"


def escape(value: str) -> str:
    return html.escape(value, quote=False)


def para(text: str = "", style: str | None = None) -> str:
    style_xml = ""
    if style:
        style_xml = f'<w:pPr><w:pStyle w:val="{style}"/></w:pPr>'
    runs: list[str] = []
    parts = re.split(r"(`[^`]+`)", text)
    for part in parts:
        if not part:
            continue
        if part.startswith("`") and part.endswith("`"):
            runs.append(
                "<w:r><w:rPr><w:rStyle w:val=\"CodeChar\"/></w:rPr>"
                f"<w:t>{escape(part[1:-1])}</w:t></w:r>"
            )
        else:
            runs.append(f"<w:r><w:t xml:space=\"preserve\">{escape(part)}</w:t></w:r>")
    return f"<w:p>{style_xml}{''.join(runs)}</w:p>"


def table(rows: list[list[str]]) -> str:
    if not rows:
        return ""
    col_count = max(len(row) for row in rows)
    grid = "".join('<w:gridCol w:w="2400"/>' for _ in range(col_count))
    body = [f"<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/><w:tblW w:w=\"0\" w:type=\"auto\"/></w:tblPr><w:tblGrid>{grid}</w:tblGrid>"]
    for row_index, row in enumerate(rows):
        body.append("<w:tr>")
        for cell in row + [""] * (col_count - len(row)):
            cell_style = "<w:pPr><w:pStyle w:val=\"TableHeader\"/></w:pPr>" if row_index == 0 else ""
            body.append(
                "<w:tc><w:tcPr><w:tcW w:w=\"2400\" w:type=\"dxa\"/></w:tcPr>"
                f"<w:p>{cell_style}<w:r><w:t xml:space=\"preserve\">{escape(cell.strip())}</w:t></w:r></w:p></w:tc>"
            )
        body.append("</w:tr>")
    body.append("</w:tbl>")
    return "".join(body)


def parse_table(lines: list[str], start: int) -> tuple[str, int]:
    rows: list[list[str]] = []
    index = start
    while index < len(lines) and lines[index].strip().startswith("|"):
        raw = lines[index].strip()
        cells = [part.strip() for part in raw.strip("|").split("|")]
        if not all(re.fullmatch(r":?-{3,}:?", cell) for cell in cells):
            rows.append(cells)
        index += 1
    return table(rows), index


def build_document_body(markdown: str) -> str:
    lines = markdown.splitlines()
    output: list[str] = []
    index = 0
    while index < len(lines):
        line = lines[index].rstrip()
        stripped = line.strip()
        if not stripped:
            output.append(para(""))
            index += 1
            continue
        if stripped.startswith("|"):
            table_xml, index = parse_table(lines, index)
            output.append(table_xml)
            continue
        if stripped.startswith("# "):
            output.append(para(stripped[2:].strip(), "Title"))
        elif stripped.startswith("## "):
            output.append(para(stripped[3:].strip(), "Heading1"))
        elif stripped.startswith("### "):
            output.append(para(stripped[4:].strip(), "Heading2"))
        elif stripped.startswith("- "):
            output.append(para(stripped[2:].strip(), "ListParagraph"))
        elif re.match(r"^\d+\. ", stripped):
            output.append(para(re.sub(r"^\d+\. ", "", stripped), "ListParagraph"))
        elif stripped.startswith("[Screenshot placeholder:"):
            output.append(para(stripped, "Placeholder"))
        else:
            output.append(para(stripped))
        index += 1
    return "".join(output)


def make_docx(source: Path, output: Path) -> None:
    markdown = source.read_text(encoding="utf-8")
    document_body = build_document_body(markdown)
    document_xml = f"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:wpc="http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math" xmlns:v="urn:schemas-microsoft-com:vml" xmlns:wp14="http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:w10="urn:schemas-microsoft-com:office:word" xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:w14="http://schemas.microsoft.com/office/word/2010/wordml" xmlns:wpg="http://schemas.microsoft.com/office/word/2010/wordprocessingGroup" xmlns:wpi="http://schemas.microsoft.com/office/word/2010/wordprocessingInk" xmlns:wne="http://schemas.microsoft.com/office/word/2006/wordml" xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape" mc:Ignorable="w14 wp14"><w:body>{document_body}<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="720" w:right="720" w:bottom="720" w:left="720" w:header="720" w:footer="720" w:gutter="0"/></w:sectPr></w:body></w:document>"""
    styles_xml = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:rPr><w:sz w:val="22"/><w:szCs w:val="22"/><w:rFonts w:ascii="Aptos" w:hAnsi="Aptos"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:rPr><w:b/><w:sz w:val="34"/><w:szCs w:val="34"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:rPr><w:b/><w:color w:val="1F4E79"/><w:sz w:val="28"/><w:szCs w:val="28"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:rPr><w:b/><w:color w:val="2F75B5"/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="ListParagraph"><w:name w:val="List Paragraph"/><w:basedOn w:val="Normal"/><w:pPr><w:ind w:left="720"/></w:pPr></w:style>
  <w:style w:type="paragraph" w:styleId="Placeholder"><w:name w:val="Screenshot Placeholder"/><w:basedOn w:val="Normal"/><w:rPr><w:i/><w:color w:val="666666"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="TableHeader"><w:name w:val="Table Header"/><w:basedOn w:val="Normal"/><w:rPr><w:b/></w:rPr></w:style>
  <w:style w:type="character" w:styleId="CodeChar"><w:name w:val="Code Char"/><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:color w:val="333333"/></w:rPr></w:style>
  <w:style w:type="table" w:styleId="TableGrid"><w:name w:val="Table Grid"/><w:tblPr><w:tblBorders><w:top w:val="single" w:sz="4" w:space="0" w:color="999999"/><w:left w:val="single" w:sz="4" w:space="0" w:color="999999"/><w:bottom w:val="single" w:sz="4" w:space="0" w:color="999999"/><w:right w:val="single" w:sz="4" w:space="0" w:color="999999"/><w:insideH w:val="single" w:sz="4" w:space="0" w:color="999999"/><w:insideV w:val="single" w:sz="4" w:space="0" w:color="999999"/></w:tblBorders></w:tblPr></w:style>
</w:styles>"""
    content_types = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/><Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/><Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/></Types>"""
    rels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>"""
    doc_rels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>"""
    core = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><dc:title>Sidwell Cadastre Tools Parcel Workflow Extension User Training and Test Guide</dc:title><dc:creator>Paige</dc:creator><cp:lastModifiedBy>Paige</cp:lastModifiedBy></cp:coreProperties>"""
    app = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"><Application>Codex</Application></Properties>"""

    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as package:
        package.writestr("[Content_Types].xml", content_types)
        package.writestr("_rels/.rels", rels)
        package.writestr("word/document.xml", document_xml)
        package.writestr("word/styles.xml", styles_xml)
        package.writestr("word/_rels/document.xml.rels", doc_rels)
        package.writestr("docProps/core.xml", core)
        package.writestr("docProps/app.xml", app)


if __name__ == "__main__":
    if not SOURCE.exists():
        raise SystemExit(f"Source not found: {SOURCE}")
    make_docx(SOURCE, OUTPUT)
    print(OUTPUT)
