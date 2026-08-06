# Builds the icon production sheet: one row per icon to draw, sorted by star then footprint.
#
# Why this exists separately from pieces.csv: that export is for balance review, this one is for an
# artist. It carries the prompt on the row, because a sheet you have to cross-reference against a
# second document is a sheet nobody uses.
#
# Safe to re-run. Reads only, writes only design\art\icon-worklist.csv.

$ErrorActionPreference = 'Stop'
$root   = Split-Path -Parent $PSScriptRoot
$pieces = Join-Path $root 'Assets\GameData\Pieces'
$icons  = Join-Path $root 'Assets\GameData\Icons'
$prompts= Join-Path $root 'design\art\ai-prompts.md'
$outDir = Join-Path $root 'design\art'
$out    = Join-Path $outDir 'icon-worklist.csv'

# --- shape index -> name and cell count, parsed from Shapes.cs so it can never drift ---
$shapesSrc = Get-Content (Join-Path $root 'Assets\Scripts\Data\Shapes.cs') -Raw

$cellCount = @{}
foreach ($m in [regex]::Matches($shapesSrc, '(?s)Vector2Int\[\]\s+(\w+)Cells\s*=\s*\{(.*?)\};')) {
    $cellCount[$m.Groups[1].Value] = ([regex]::Matches($m.Groups[2].Value, 'new Vector2Int')).Count
}

# The enum body carries inline comments, so take identifiers only and keep declaration order.
$shapeOrder = @()
if ($shapesSrc -match '(?s)enum\s+ShapeKind\s*\{(.*?)\}') {
    foreach ($tok in ($Matches[1] -split "[,\r\n]")) {
        $tok = ($tok -replace '//.*$', '').Trim()
        if ($tok -match '^[A-Za-z][A-Za-z0-9]*$' -and $cellCount.ContainsKey($tok)) { $shapeOrder += $tok }
    }
}

$elements = @('Fire', 'Ice', 'Lightning', 'Arcane')
$kinds    = @('Projectile', 'Nova', 'Chain', 'Heal', 'AreaAtTarget', 'Line', 'Zone', 'Passive', 'AuraOnly', 'Cleanse', 'Radial')

# --- prompts, keyed by the icon filename they produce ---
$promptFor = @{}
if (Test-Path $prompts) {
    foreach ($line in Get-Content $prompts) {
        if ($line -notmatch '^\s*\|') { continue }
        # The file cell is wrapped in backticks in the prompt book, so strip them before matching.
        $cols = ($line -split '\|') | ForEach-Object { $_.Trim().Trim('`').Trim() }
        $file = $cols | Where-Object { $_ -match '^Icon_[\w]+\.png$' } | Select-Object -First 1
        if (-not $file) { continue }
        # the prompt is the longest cell on the row
        $text = ($cols | Sort-Object { $_.Length } -Descending | Select-Object -First 1)
        if ($text.Length -gt 40) { $promptFor[$file] = $text }
    }
}

function Field($text, $name) {
    # Unity indents MonoBehaviour fields by two spaces, so this must not anchor hard to column 0.
    # It also must not match a longer field that merely starts with the same word.
    if ($text -match "(?m)^\s*$name\s*:[ \t]*(.*?)\s*$") { return $Matches[1] }
    return ''
}

$rows = foreach ($f in Get-ChildItem $pieces -Filter *.asset) {
    $t = Get-Content $f.FullName -Raw

    $id    = Field $t 'Id'
    $stars = [int](Field $t 'Stars')
    $shapeIndex = [int](Field $t 'Shape')
    $kindIndex  = [int](Field $t 'Kind')
    $layerIndex = [int](Field $t 'Layer')
    $elemIndex  = [int](Field $t 'Element')

    $shape = if ($shapeIndex -lt $shapeOrder.Count) { $shapeOrder[$shapeIndex] } else { "?$shapeIndex" }
    $kind  = if ($kindIndex  -lt $kinds.Count)      { $kinds[$kindIndex] }       else { "?$kindIndex" }
    $elem  = if ($elemIndex  -lt $elements.Count)   { $elements[$elemIndex] }    else { "?$elemIndex" }

    # A passive skill is a sigil; the code has no separate layer for it.
    $layer = if ($layerIndex -eq 0) { 'Rune' } elseif ($kind -eq 'Passive') { 'Segel' } else { 'Skill' }

    $iconFile = "Icon_$id.png"
    $hasIcon  = Test-Path (Join-Path $icons $iconFile)

    [PSCustomObject]@{
        Bintang   = $stars
        Petak     = $(if ($cellCount.ContainsKey($shape)) { $cellCount[$shape] } else { 0 })
        Layer     = $layer
        Elemen    = $elem
        Bentuk    = $shape
        Nama      = Field $t 'DisplayName'
        Id        = $id
        FileIkon  = $iconFile
        Status    = $(if ($hasIcon) { 'placeholder' } else { 'BELUM ADA' })
        Selesai   = ''
        Kind      = $kind
        Deskripsi = Field $t 'Blurb'
        Prompt    = $(if ($promptFor.ContainsKey($iconFile)) { $promptFor[$iconFile] } else { '' })
        PathIkon  = Join-Path $icons $iconFile
    }
}

# Star first, then footprint: an artist wants to draw the small cheap ones as a warm-up, and it
# groups pieces that will sit side by side on the board.
$sorted = $rows | Sort-Object Bintang, Petak, Layer, Elemen, Nama

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory $outDir | Out-Null }

$csv = $sorted | ConvertTo-Csv -NoTypeInformation
# "sep=," makes Excel parse commas even on locales whose list separator is a semicolon.
$text = "sep=,`r`n" + ($csv -join "`r`n") + "`r`n"
[System.IO.File]::WriteAllText($out, $text, (New-Object System.Text.UTF8Encoding $true))

# --- HTML twin, because a spreadsheet cannot show you the icon you are replacing ---
# Icons are embedded as data URIs so the file can be moved, mailed or opened from anywhere
# without dragging the Icons folder along.
$outHtml = Join-Path $outDir 'icon-worklist.html'
$sb = New-Object System.Text.StringBuilder

[void]$sb.AppendLine(@'
<!doctype html><meta charset="utf-8"><title>Icon worklist</title>
<style>
 body{background:#14100c;color:#e8d9b0;font:13px/1.45 system-ui,sans-serif;margin:24px}
 h1{font-size:19px;margin:0 0 4px} p.sub{color:#9a8f78;margin:0 0 18px}
 table{border-collapse:collapse;width:100%}
 th{position:sticky;top:0;background:#211a13;text-align:left;padding:8px;font-weight:600;border-bottom:2px solid #3a2f22}
 td{padding:7px 8px;border-bottom:1px solid #251e16;vertical-align:top}
 tr:hover td{background:#1c1610}
 img{width:52px;height:52px;image-rendering:pixelated;background:#0d0a07;border-radius:6px;display:block}
 .none{width:52px;height:52px;border:1px dashed #6a4a3a;border-radius:6px;color:#c0705a;
       display:flex;align-items:center;justify-content:center;font-size:10px}
 .star{color:#d9a441;white-space:nowrap} .cells{color:#8fb9d9}
 .Rune{color:#c08a5a} .Skill{color:#e8d9b0} .Segel{color:#9d86d9}
 .prompt{color:#8d8474;max-width:520px;font-size:12px}
 .id{color:#6f6657;font-family:ui-monospace,monospace;font-size:11px}
</style>
<h1>Icon worklist</h1>
<p class="sub">Diurut bintang, lalu jumlah petak. Gambar di kolom kiri adalah placeholder yang sekarang terpasang &mdash; itu yang mau kamu ganti.</p>
<table><thead><tr>
<th>Sekarang</th><th>&#9733;</th><th>Petak</th><th>Nama</th><th>Layer</th><th>Elemen</th><th>Bentuk</th><th>File</th><th>Prompt</th>
</tr></thead><tbody>
'@)

foreach ($row in $sorted) {
    if (Test-Path $row.PathIkon) {
        $b64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($row.PathIkon))
        $img = "<img src='data:image/png;base64,$b64' alt=''>"
    } else {
        $img = "<div class='none'>belum</div>"
    }

    $esc = { param($s) if ($null -eq $s) { '' } else { $s -replace '&', '&amp;' -replace '<', '&lt;' -replace '>', '&gt;' } }

    [void]$sb.AppendLine("<tr><td>$img</td>" +
        "<td class='star'>" + ('&#9733;' * $row.Bintang) + "</td>" +
        "<td class='cells'>$($row.Petak)</td>" +
        "<td><b>$(& $esc $row.Nama)</b><br><span class='id'>$($row.Id)</span></td>" +
        "<td class='$($row.Layer)'>$($row.Layer)</td>" +
        "<td>$($row.Elemen)</td><td>$($row.Bentuk)</td>" +
        "<td class='id'>$($row.FileIkon)</td>" +
        "<td class='prompt'>$(& $esc $row.Prompt)</td></tr>")
}

[void]$sb.AppendLine('</tbody></table>')
[System.IO.File]::WriteAllText($outHtml, $sb.ToString(), (New-Object System.Text.UTF8Encoding $false))

Write-Host "$($sorted.Count) baris -> $out"
Write-Host "                 -> $outHtml"
$sorted | Group-Object Bintang | ForEach-Object {
    $missing = ($_.Group | Where-Object { $_.Status -eq 'BELUM ADA' }).Count
    $noPrompt = ($_.Group | Where-Object { $_.Prompt -eq '' }).Count
    "  bintang $($_.Name): $($_.Count) ikon, $missing belum ada file, $noPrompt tanpa prompt"
}
