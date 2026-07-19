param(
    [string]$OutputPath = "_bmad-output/training-artifacts/nla-plan-examination-4-day-training-overview.pptx"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression

$root = Join-Path ([System.IO.Path]::GetTempPath()) ("nla-pe-training-pptx-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $root | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "_rels") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "docProps") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "ppt/_rels") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "ppt/slides") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "ppt/slideMasters/_rels") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "ppt/slideLayouts") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "ppt/theme") | Out-Null

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }

    [System.IO.File]::WriteAllText((Resolve-Path -LiteralPath $dir).Path + "\" + (Split-Path -Leaf $Path), $Content, [System.Text.UTF8Encoding]::new($false))
}

function Escape-Xml {
    param([string]$Value)
    return [System.Security.SecurityElement]::Escape($Value)
}

function Emu {
    param([double]$Px)
    return [int][Math]::Round($Px * 9525)
}

function ColorHex {
    param([string]$Color)
    return $Color.TrimStart("#")
}

function TextBoxXml {
    param(
        [int]$Id,
        [string]$Name,
        [double]$X,
        [double]$Y,
        [double]$W,
        [double]$H,
        [string[]]$Lines,
        [double]$FontSize = 20,
        [string]$Color = "#111111",
        [bool]$Bold = $false,
        [string]$Align = "l"
    )

    $paragraphs = foreach ($line in $Lines) {
        $safe = Escape-Xml $line
        $boldAttr = if ($Bold) { ' b="1"' } else { "" }
        "<a:p><a:pPr algn=`"$Align`"/><a:r><a:rPr lang=`"en-US`" sz=`"$([int]($FontSize * 100))`"$boldAttr><a:solidFill><a:srgbClr val=`"$(ColorHex $Color)`"/></a:solidFill><a:latin typeface=`"Arial`"/></a:rPr><a:t>$safe</a:t></a:r><a:endParaRPr lang=`"en-US`" sz=`"$([int]($FontSize * 100))`"/></a:p>"
    }

    @"
<p:sp>
  <p:nvSpPr><p:cNvPr id="$Id" name="$(Escape-Xml $Name)"/><p:cNvSpPr txBox="1"/><p:nvPr/></p:nvSpPr>
  <p:spPr>
    <a:xfrm><a:off x="$(Emu $X)" y="$(Emu $Y)"/><a:ext cx="$(Emu $W)" cy="$(Emu $H)"/></a:xfrm>
    <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
    <a:noFill/><a:ln><a:noFill/></a:ln>
  </p:spPr>
  <p:txBody><a:bodyPr wrap="square" anchor="t"/><a:lstStyle/>$($paragraphs -join "")</p:txBody>
</p:sp>
"@
}

function RectXml {
    param(
        [int]$Id,
        [string]$Name,
        [double]$X,
        [double]$Y,
        [double]$W,
        [double]$H,
        [string]$Fill = "#EDEDED",
        [string]$Line = "#B8BCC4",
        [double]$LineWidth = 1
    )

    @"
<p:sp>
  <p:nvSpPr><p:cNvPr id="$Id" name="$(Escape-Xml $Name)"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
  <p:spPr>
    <a:xfrm><a:off x="$(Emu $X)" y="$(Emu $Y)"/><a:ext cx="$(Emu $W)" cy="$(Emu $H)"/></a:xfrm>
    <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
    <a:solidFill><a:srgbClr val="$(ColorHex $Fill)"/></a:solidFill>
    <a:ln w="$([int]($LineWidth * 12700))"><a:solidFill><a:srgbClr val="$(ColorHex $Line)"/></a:solidFill></a:ln>
  </p:spPr>
  <p:txBody><a:bodyPr/><a:lstStyle/><a:p/></p:txBody>
</p:sp>
"@
}

function LineXml {
    param(
        [int]$Id,
        [string]$Name,
        [double]$X,
        [double]$Y,
        [double]$W,
        [double]$H,
        [string]$Line = "#111111",
        [double]$LineWidth = 1.5
    )

    @"
<p:cxnSp>
  <p:nvCxnSpPr><p:cNvPr id="$Id" name="$(Escape-Xml $Name)"/><p:cNvCxnSpPr/><p:nvPr/></p:nvCxnSpPr>
  <p:spPr>
    <a:xfrm><a:off x="$(Emu $X)" y="$(Emu $Y)"/><a:ext cx="$(Emu $W)" cy="$(Emu $H)"/></a:xfrm>
    <a:prstGeom prst="line"><a:avLst/></a:prstGeom>
    <a:ln w="$([int]($LineWidth * 12700))"><a:solidFill><a:srgbClr val="$(ColorHex $Line)"/></a:solidFill></a:ln>
  </p:spPr>
</p:cxnSp>
"@
}

function SlideXml {
    param([string]$Body)
    @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sld xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:cSld>
    <p:bg><p:bgPr><a:solidFill><a:srgbClr val="FFFFFF"/></a:solidFill><a:effectLst/></p:bgPr></p:bg>
    <p:spTree>
      <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
      <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>
      $Body
    </p:spTree>
  </p:cSld>
  <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
</p:sld>
"@
}

function SlideTitle {
    param([int]$Id, [string]$Title)
    return TextBoxXml -Id $Id -Name "Slide title" -X 48 -Y 40 -W 850 -H 92 -Lines @($Title) -FontSize 38 -Bold $true
}

function SlideFooter {
    param([int]$Id, [string]$Label)
    return TextBoxXml -Id $Id -Name "Footer" -X 48 -Y 666 -W 800 -H 28 -Lines @($Label) -FontSize 12 -Color "#666666"
}

$slides = @()

$slides += @{
    Title = "Plan Examination training starts with Pro fluency, then transaction confidence"
    Body = @(
        (TextBoxXml 2 "Eyebrow" 48 48 650 28 @("NLA e-Titling | ArcGIS Pro and Cadastre Tools") 16 "#555555" $true),
        (TextBoxXml 3 "Main title" 48 162 980 170 @("Plan Examination", "4-day training overview") 52 "#000000" $true),
        (TextBoxXml 4 "Subtitle" 52 400 720 110 @("Two days establish the ArcGIS Pro working model. Two days apply that model to the Plan Examination transaction and the Sidwell Plan Extension tools.") 22 "#333333"),
        (RectXml 5 "Right rail" 960 80 220 520 "#F0F3F6" "#F0F3F6"),
        (TextBoxXml 6 "Right rail text" 990 140 165 320 @("ArcMap to ArcGIS Pro", "Transaction workflow", "Compute review", "Compare evidence", "Reports and handoff") 22 "#111111" $true),
        (SlideFooter 7 "Draft training deck | Prepared for initial planning")
    ) -join ""
}

$slides += @{
    Title = "The four days build from platform habits to examination decisions"
    Body = @(
        (SlideTitle 2 "The four days build from platform habits to examination decisions"),
        (RectXml 3 "D1" 62 180 270 350 "#F4F4F4" "#B8BCC4"),
        (RectXml 4 "D2" 358 180 270 350 "#F4F4F4" "#B8BCC4"),
        (RectXml 5 "D3" 654 180 270 350 "#FFF2EA" "#FF6B35"),
        (RectXml 6 "D4" 950 180 270 350 "#FFF2EA" "#FF6B35"),
        (TextBoxXml 7 "D1 text" 88 210 220 260 @("Day 1", "ArcGIS Pro foundations", "Unit 1 introduces projects, maps, panes, data, navigation, selection, joins, labels, and symbology. Unit 2 introduces editing.") 22 "#000000" $true),
        (TextBoxXml 8 "D2 text" 384 210 220 260 @("Day 2", "Production workflows", "Units 3-5 cover layouts, map series, parcel fabric concepts, custom ribbons, and ArcGIS Pro Tasks.") 22 "#000000" $true),
        (TextBoxXml 9 "D3 text" 680 210 220 260 @("Day 3", "Plan Examination Compute", "Open the transaction, review documents, validate spatial data, correct evidence, and create review outputs.") 22 "#000000" $true),
        (TextBoxXml 10 "D4 text" 976 210 220 260 @("Day 4", "Compare and closeout", "Reconcile ownership evidence, retain findings, generate reports, and finalize the transaction stage.") 22 "#000000" $true),
        (SlideFooter 11 "Training path")
    ) -join ""
}

$slides += @{
    Title = "The opening session connects the product, process, and transaction"
    Body = @(
        (SlideTitle 2 "The opening session connects the product, process, and transaction"),
        (RectXml 3 "Agenda quote" 78 190 1120 160 "#F4F4F4" "#B8BCC4"),
        (TextBoxXml 4 "Agenda copy" 112 218 1038 92 @("SMD Business Processes and Solution Overview", "Provides an overview of ArcGIS Pro and the Sidwell Plan Extension tools and illustrates how SMD transactions are processed within the ArcGIS Pro examination environment.") 24 "#111111" $true),
        (TextBoxXml 5 "Interpretation" 90 410 1030 120 @("This is the bridge: participants should understand not only where tools are located, but why the examination workflow has been redesigned around ArcGIS Pro, Enterprise layers, and structured transaction evidence.") 25 "#333333"),
        (LineXml 6 "Rule" 90 382 1020 0 "#FF6B35" 2),
        (SlideFooter 7 "Overview agenda framing")
    ) -join ""
}

$slides += @{
    Title = "The ArcGIS Pro syllabus is organized into five hands-on units"
    Body = @(
        (SlideTitle 2 "The ArcGIS Pro syllabus is organized into five hands-on units"),
        (RectXml 3 "Panel 1" 54 172 220 395 "#F4F4F4" "#B8BCC4"),
        (RectXml 4 "Panel 2" 302 172 220 395 "#F4F4F4" "#B8BCC4"),
        (RectXml 5 "Panel 3" 550 172 220 395 "#F4F4F4" "#B8BCC4"),
        (RectXml 6 "Panel 4" 798 172 220 395 "#F4F4F4" "#B8BCC4"),
        (RectXml 7 "Panel 5" 1046 172 180 395 "#F4F4F4" "#B8BCC4"),
        (TextBoxXml 8 "Panel 1 text" 76 208 176 280 @("Unit 1", "Introduction to ArcGIS Pro", "Project creation, Catalog, Contents, data loading, navigation, measurements, selections, joins, labels, and symbology.") 18 "#111111" $true),
        (TextBoxXml 9 "Panel 2 text" 324 208 176 280 @("Unit 2", "Editing in ArcGIS Pro", "Edit tools, line work, attributes, polygons, traverse, copying, moving, and quality-aware edit habits.") 18 "#111111" $true),
        (TextBoxXml 10 "Panel 3 text" 572 208 176 280 @("Unit 3", "Layouts", "Map layouts, map frames, scale, north arrow, scale bar, images, map series, and export-ready outputs.") 18 "#111111" $true),
        (TextBoxXml 11 "Panel 4 text" 820 208 176 280 @("Unit 4", "Parcel Fabric", "Conceptual introduction to parcel fabric layers, records, topology, workflows, and branch versioning.") 18 "#111111" $true),
        (TextBoxXml 12 "Panel 5 text" 1066 208 138 280 @("Unit 5", "Customizations and Tasks", "Quick Access Toolbar, custom ribbons, task execution, and task design.") 18 "#111111" $true),
        (SlideFooter 9 "ArcGIS Pro foundation")
    ) -join ""
}

$slides += @{
    Title = "Day 1 focuses on confidence in the ArcGIS Pro workspace"
    Body = @(
        (SlideTitle 2 "Day 1 focuses on confidence in the ArcGIS Pro workspace"),
        (TextBoxXml 3 "Left" 62 172 440 370 @("Core topics from Unit 1", "- Create a new project and save it", "- Use Catalog, Contents, folders, favorites, and geodatabases", "- Add parish and parcel data to a map", "- Set definition queries and rename/reorder layers", "- Navigate, locate parcels, measure, select, join, label, and symbolize") 21 "#111111"),
        (RectXml 4 "Right panel" 580 170 600 360 "#F4F4F4" "#B8BCC4"),
        (TextBoxXml 5 "Right" 620 220 520 240 @("Participant outcome", "By the end of the first block, participants should be comfortable opening ArcGIS Pro, managing project resources, reading parcel layers, and using the basic map, table, and selection tools needed for examination work.") 27 "#111111" $true),
        (SlideFooter 6 "Day 1")
    ) -join ""
}

$slides += @{
    Title = "Day 2 turns Pro familiarity into parcel production habits"
    Body = @(
        (SlideTitle 2 "Day 2 turns Pro familiarity into parcel production habits"),
        (RectXml 3 "One" 68 180 250 330 "#F4F4F4" "#B8BCC4"),
        (RectXml 4 "Two" 374 180 250 330 "#F4F4F4" "#B8BCC4"),
        (RectXml 5 "Three" 680 180 250 330 "#F4F4F4" "#B8BCC4"),
        (RectXml 6 "Four" 986 180 220 330 "#F4F4F4" "#B8BCC4"),
        (TextBoxXml 7 "One text" 94 220 200 230 @("Edit", "Use Unit 2 to practice lines, attributes, polygons, traverse, copy, move, and mirror-style edit thinking.") 22 "#000000" $true),
        (TextBoxXml 8 "Two text" 400 220 200 230 @("Layout", "Use Unit 3 to prepare layouts, map frames, scale bars, north arrows, images, map series, and exports.") 22 "#000000" $true),
        (TextBoxXml 9 "Three text" 706 220 200 230 @("Parcel fabric", "Use Unit 4 to introduce parcel fabric concepts, records, topology, workflows, and branch versioning.") 22 "#000000" $true),
        (TextBoxXml 10 "Four text" 1012 220 170 230 @("Tasks", "Use Unit 5 to connect Pro customization and Tasks to guided examination work.") 22 "#000000" $true),
        (SlideFooter 11 "Day 2")
    ) -join ""
}

$slides += @{
    Title = "Days 3 and 4 apply Pro skills to the Plan Examination transaction"
    Body = @(
        (SlideTitle 2 "Days 3 and 4 apply Pro skills to the Plan Examination transaction"),
        (LineXml 3 "Workflow line" 120 360 1040 0 "#B8BCC4" 2),
        (RectXml 4 "Step 1" 90 282 210 155 "#FFF2EA" "#FF6B35"),
        (RectXml 5 "Step 2" 350 282 210 155 "#FFF2EA" "#FF6B35"),
        (RectXml 6 "Step 3" 610 282 210 155 "#FFF2EA" "#FF6B35"),
        (RectXml 7 "Step 4" 870 282 260 155 "#FFF2EA" "#FF6B35"),
        (TextBoxXml 8 "Step 1 text" 112 315 165 72 @("Load transaction", "Open the PE task and attached documents.") 20 "#000000" $true),
        (TextBoxXml 9 "Step 2 text" 372 315 165 72 @("Compute", "Validate source evidence and spatial geometry.") 20 "#000000" $true),
        (TextBoxXml 10 "Step 3 text" 632 315 165 72 @("Compare", "Reconcile ownership and cadastre evidence.") 20 "#000000" $true),
        (TextBoxXml 11 "Step 4 text" 895 315 215 72 @("Finalize", "Generate reports and move to the next stage.") 20 "#000000" $true),
        (TextBoxXml 12 "Bottom" 110 500 980 70 @("The Plan Examination training is scenario-led: participants follow a transaction through the same decision points they will use after go-live.") 24 "#333333"),
        (SlideFooter 13 "Plan Examination workflow")
    ) -join ""
}

$slides += @{
    Title = "Day 3 teaches the Compute stage as evidence preparation"
    Body = @(
        (SlideTitle 2 "Day 3 teaches the Compute stage as evidence preparation"),
        (TextBoxXml 3 "Left copy" 62 170 470 420 @("What participants practice", "- Select and open a Plan Examination transaction", "- Review attached survey plan documents", "- Understand supporting document, structure, georeference, and dimension checks", "- Validate extracted points and lines", "- Create spatial units and review generated outputs") 22 "#111111"),
        (RectXml 4 "Right panel" 620 168 540 400 "#F4F4F4" "#B8BCC4"),
        (TextBoxXml 5 "Right copy" 660 215 460 255 @("Compute is not just a checklist", "It turns source documents and extracted geometry into a defensible spatial review package. The examiner learns when to trust extraction, when to correct it, and when a blocker must remain visible.") 27 "#000000" $true),
        (SlideFooter 6 "Day 3")
    ) -join ""
}

$slides += @{
    Title = "Day 4 teaches Compare as evidence reconciliation"
    Body = @(
        (SlideTitle 2 "Day 4 teaches Compare as evidence reconciliation"),
        (RectXml 3 "Left" 68 176 350 380 "#F4F4F4" "#B8BCC4"),
        (RectXml 4 "Center" 466 176 350 380 "#F4F4F4" "#B8BCC4"),
        (RectXml 5 "Right" 864 176 350 380 "#F4F4F4" "#B8BCC4"),
        (TextBoxXml 6 "Left text" 98 215 290 255 @("Evidence sources", "Survey plan PDF, working_review geometry, Legal Cadastre, Fiscal Cadastre, and Innola ownership searches.") 24 "#000000" $true),
        (TextBoxXml 7 "Center text" 496 215 290 255 @("Examiner action", "Search by Volume/Folio, PID, Land Val No., or owner name; keep valuable evidence; record notes.") 24 "#000000" $true),
        (TextBoxXml 8 "Right text" 894 215 290 255 @("Closeout", "Save the review, generate the Compare report, suspend if needed, or finalize when the evidence supports the decision.") 24 "#000000" $true),
        (SlideFooter 9 "Day 4")
    ) -join ""
}

$slides += @{
    Title = "The Sidwell Plan Extension keeps examination work structured"
    Body = @(
        (SlideTitle 2 "The Sidwell Plan Extension keeps examination work structured"),
        (TextBoxXml 3 "Left" 70 172 490 390 @("What the add-in contributes", "- Transaction list and stage routing", "- Attached document loading and PDF review", "- Compute checks and spatial-unit outputs", "- Compare workspace for Legal/Fiscal evidence", "- Save, suspend, finalize, and report generation") 23 "#111111"),
        (RectXml 4 "Right" 650 172 480 360 "#FFF2EA" "#FF6B35"),
        (TextBoxXml 5 "Right text" 690 230 400 210 @("Training message", "The tools should feel like a guided examination workspace, not a separate GIS project to manage manually.") 32 "#000000" $true),
        (SlideFooter 6 "Sidwell Plan Extension")
    ) -join ""
}

$slides += @{
    Title = "Hands-on practice should follow real examiner decisions"
    Body = @(
        (SlideTitle 2 "Hands-on practice should follow real examiner decisions"),
        (RectXml 3 "Lab 1" 70 175 320 380 "#F4F4F4" "#B8BCC4"),
        (RectXml 4 "Lab 2" 480 175 320 380 "#F4F4F4" "#B8BCC4"),
        (RectXml 5 "Lab 3" 890 175 320 380 "#F4F4F4" "#B8BCC4"),
        (TextBoxXml 6 "Lab 1 text" 100 215 260 245 @("Lab 1", "Open and inspect", "Participants load the transaction, documents, map layers, and evidence context.") 23 "#000000" $true),
        (TextBoxXml 7 "Lab 2 text" 510 215 260 245 @("Lab 2", "Resolve or retain", "Participants correct what can be corrected and preserve blockers that need review.") 23 "#000000" $true),
        (TextBoxXml 8 "Lab 3 text" 920 215 260 245 @("Lab 3", "Save or finalize", "Participants generate reports and understand when to suspend or complete the task.") 23 "#000000" $true),
        (SlideFooter 9 "Suggested lab pattern")
    ) -join ""
}

$slides += @{
    Title = "Successful training means participants can explain and execute the workflow"
    Body = @(
        (SlideTitle 2 "Successful training means participants can explain and execute the workflow"),
        (TextBoxXml 3 "Success list" 90 180 1060 360 @("After four days, participants should be able to:", "- Work confidently in ArcGIS Pro without relying on ArcMap mental shortcuts.", "- Open the Plan Examination transaction and understand the current stage.", "- Review attached evidence, map context, and extracted geometry.", "- Use Compare to keep ownership evidence and record notes.", "- Save, suspend, or finalize with awareness of report and task state.") 25 "#111111"),
        (LineXml 4 "Accent" 90 565 760 0 "#FF6B35" 3),
        (SlideFooter 5 "Training outcomes")
    ) -join ""
}

$slides += @{
    Title = "References used to frame the ArcGIS Pro portion"
    Body = @(
        (SlideTitle 2 "References used to frame the ArcGIS Pro portion"),
        (TextBoxXml 3 "Refs" 76 170 1080 390 @("Training syllabus PDFs", "- Unit 1 - Intro to ArcGIS Pro", "- Unit 2 - Editing in ArcGIS Pro", "- Unit 3 - Layouts in ArcGIS Pro", "- Unit 4 - Parcel Fabric", "- Unit 5 - Customizations and Tasks in ArcGIS Pro", "", "Esri ArcGIS Pro documentation", "- ArcMap migration guidance, ArcMap document import, quick-start tutorials, and ArcGIS Pro 3.6 release notes", "", "Project context", "- Sidwell Plan Extension story artifacts for Compute, Compare, Enterprise Legal/Fiscal evidence, and Compare lifecycle/reporting") 20 "#111111"),
        (SlideFooter 4 "Sources")
    ) -join ""
}

for ($i = 0; $i -lt $slides.Count; $i++) {
    $slideNumber = $i + 1
    Write-Utf8NoBom -Path (Join-Path $root "ppt/slides/slide$slideNumber.xml") -Content (SlideXml $slides[$i].Body)
}

$slideOverrides = (1..$slides.Count | ForEach-Object { "<Override PartName=`"/ppt/slides/slide$_.xml`" ContentType=`"application/vnd.openxmlformats-officedocument.presentationml.slide+xml`"/>" }) -join ""

Write-Utf8NoBom -Path (Join-Path $root "[Content_Types].xml") -Content @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
  <Override PartName="/ppt/presentation.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"/>
  <Override PartName="/ppt/slideMasters/slideMaster1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml"/>
  <Override PartName="/ppt/slideLayouts/slideLayout1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml"/>
  <Override PartName="/ppt/theme/theme1.xml" ContentType="application/vnd.openxmlformats-officedocument.theme+xml"/>
  $slideOverrides
</Types>
"@

$slideIds = (1..$slides.Count | ForEach-Object { "<p:sldId id=`"$([uint32](255 + $_))`" r:id=`"rId$([int]($_ + 1))`"/>" }) -join ""
$slideRels = (1..$slides.Count | ForEach-Object { "<Relationship Id=`"rId$([int]($_ + 1))`" Type=`"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide`" Target=`"slides/slide$_.xml`"/>" }) -join ""

Write-Utf8NoBom -Path (Join-Path $root "_rels/.rels") -Content @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
"@

Write-Utf8NoBom -Path (Join-Path $root "docProps/core.xml") -Content @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>NLA Plan Examination 4-Day Training Overview</dc:title>
  <dc:creator>Codex / Paige</dc:creator>
  <cp:lastModifiedBy>Codex / Paige</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">2026-07-19T00:00:00Z</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">2026-07-19T00:00:00Z</dcterms:modified>
</cp:coreProperties>
"@

Write-Utf8NoBom -Path (Join-Path $root "docProps/app.xml") -Content @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>Microsoft PowerPoint</Application>
  <PresentationFormat>On-screen Show (16:9)</PresentationFormat>
  <Slides>$($slides.Count)</Slides>
  <Company>Sidwell / NLA</Company>
</Properties>
"@

Write-Utf8NoBom -Path (Join-Path $root "ppt/presentation.xml") -Content @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:presentation xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:sldMasterIdLst><p:sldMasterId id="2147483648" r:id="rId1"/></p:sldMasterIdLst>
  <p:sldIdLst>$slideIds</p:sldIdLst>
  <p:sldSz cx="12192000" cy="6858000" type="screen16x9"/>
  <p:notesSz cx="6858000" cy="9144000"/>
  <p:defaultTextStyle/>
</p:presentation>
"@

Write-Utf8NoBom -Path (Join-Path $root "ppt/_rels/presentation.xml.rels") -Content @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="slideMasters/slideMaster1.xml"/>
  $slideRels
</Relationships>
"@

Write-Utf8NoBom -Path (Join-Path $root "ppt/slideMasters/slideMaster1.xml") -Content @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sldMaster xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr></p:spTree></p:cSld>
  <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2" accent1="accent1" accent2="accent2" accent3="accent3" accent4="accent4" accent5="accent5" accent6="accent6" hlink="hlink" folHlink="folHlink"/>
  <p:sldLayoutIdLst><p:sldLayoutId id="2147483649" r:id="rId1"/></p:sldLayoutIdLst>
  <p:txStyles><p:titleStyle/><p:bodyStyle/><p:otherStyle/></p:txStyles>
</p:sldMaster>
"@

Write-Utf8NoBom -Path (Join-Path $root "ppt/slideMasters/_rels/slideMaster1.xml.rels") -Content @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml"/>
</Relationships>
"@

Write-Utf8NoBom -Path (Join-Path $root "ppt/slideLayouts/slideLayout1.xml") -Content @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sldLayout xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" type="blank" preserve="1">
  <p:cSld name="Blank"><p:spTree><p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr></p:spTree></p:cSld>
  <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
</p:sldLayout>
"@

Write-Utf8NoBom -Path (Join-Path $root "ppt/theme/theme1.xml") -Content @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="NLA Training">
  <a:themeElements>
    <a:clrScheme name="NLA Training"><a:dk1><a:srgbClr val="000000"/></a:dk1><a:lt1><a:srgbClr val="FFFFFF"/></a:lt1><a:dk2><a:srgbClr val="333333"/></a:dk2><a:lt2><a:srgbClr val="F4F4F4"/></a:lt2><a:accent1><a:srgbClr val="FF6B35"/></a:accent1><a:accent2><a:srgbClr val="B8BCC4"/></a:accent2><a:accent3><a:srgbClr val="555555"/></a:accent3><a:accent4><a:srgbClr val="111111"/></a:accent4><a:accent5><a:srgbClr val="EDEDED"/></a:accent5><a:accent6><a:srgbClr val="FFF2EA"/></a:accent6><a:hlink><a:srgbClr val="0563C1"/></a:hlink><a:folHlink><a:srgbClr val="954F72"/></a:folHlink></a:clrScheme>
    <a:fontScheme name="Arial"><a:majorFont><a:latin typeface="Arial"/></a:majorFont><a:minorFont><a:latin typeface="Arial"/></a:minorFont></a:fontScheme>
    <a:fmtScheme name="Simple"><a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:fillStyleLst><a:lnStyleLst><a:ln w="9525"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme>
  </a:themeElements>
</a:theme>
"@

$outFull = Join-Path (Get-Location) $OutputPath
if (Test-Path $outFull) {
    Remove-Item -LiteralPath $outFull -Force
}

if (Test-Path $outFull) {
    Remove-Item -LiteralPath $outFull -Force
}

$zip = [System.IO.Compression.ZipFile]::Open($outFull, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object {
        $relative = $_.FullName.Substring($root.Length).TrimStart("\")
        $entryName = $relative -replace "\\", "/"
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entryName) | Out-Null
    }
}
finally {
    $zip.Dispose()
}
Remove-Item -LiteralPath $root -Recurse -Force
Write-Host $outFull
