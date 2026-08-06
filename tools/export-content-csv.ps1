# export-content-csv.ps1 — Export Unity ScriptableObject content to Excel-friendly CSVs.
# PowerShell 5.1. Parses Unity YAML directly; enum indices mapped from Assets/Scripts/Data/Enums.cs.
# Output: design/data/*.csv (UTF-8 BOM, first line "sep=,").

$ErrorActionPreference = 'Stop'
$inv = [Globalization.CultureInfo]::InvariantCulture

$root   = 'D:\GameGamma\Project_S1(fix)'
$gd     = Join-Path $root 'Assets\GameData'
$outDir = Join-Path $root 'design\data'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force $outDir | Out-Null }

# ---------- enum tables (source of truth: Assets/Scripts/Data/Enums.cs + Shapes.cs) ----------
$ElementNames  = @('Fire','Ice','Lightning','Arcane')
$CastKindNames = @('Projectile','Nova','Chain','Heal','AreaAtTarget','Line','Zone','Passive',
                   'AuraOnly','Cleanse','Radial','Detonate','Orbit','Blink','Ward','Surge',
                   'Restore','SunStrike','RollingBall','Vortex','ForcePush')
$TriggerNames  = @('Cooldown','StatusThreshold')
$AuraNames     = @('None','DamagePct','CooldownPct','RadiusPct')
$LayerNames    = @('Rune','Skill')
$StatKindNames = @('None','MaxHp','HpRegen','Defense','MaxMana','ManaRegen','ManaCostPct',
                   'DamagePct','CooldownPct','AreaPct','RangePct','CritChance','CritDamage',
                   'FireDamagePct','IceDamagePct','LightningDamagePct','AilmentPoints',
                   'MoveSpeed','DebuffResist','Count')
# ShapeKind order from Shapes.cs; labels = Shapes.NameOf, cells = footprint size
$ShapeNames = @('TITIK','GARIS 2','GARIS 3','KOTAK 2x2','BLOK 3x3','SIKU','BENGKOK S','HURUF T',
                'HURUF L','SALIB','CAWAN','SUDUT V','LEMPENG 3x2','HURUF C','CINCIN','BONGKAH',
                'HURUF Z','HURUF H','HURUF S','TRISULA')
$ShapeCells = @(1,2,3,4,9,3,4,4,4,5,5,5,6,7,8,8,7,7,6,6)

function EN($arr, $v) {
    $i = 0
    if (-not [int]::TryParse([string]$v, [ref]$i)) { return "?$v" }
    if ($i -ge 0 -and $i -lt $arr.Count) { return $arr[$i] }
    return "?$v"
}

# ---------- YAML helpers ----------
function Unquote([string]$s) {
    if ($null -eq $s) { return '' }
    if ($s.Length -ge 2 -and $s.StartsWith('"') -and $s.EndsWith('"')) {
        $s = $s.Substring(1, $s.Length - 2)
        $s = $s.Replace('\\', [string][char]1)
        $s = [regex]::Replace($s, '\\u([0-9a-fA-F]{4})', { param($m) [string][char][Convert]::ToInt32($m.Groups[1].Value, 16) })
        $s = $s.Replace('\"', '"').Replace('\n', "`n").Replace('\t', "`t")
        $s = $s.Replace([string][char]1, '\')
    }
    elseif ($s.Length -ge 2 -and $s.StartsWith("'") -and $s.EndsWith("'")) {
        $s = $s.Substring(1, $s.Length - 2).Replace("''", "'")
    }
    return $s
}

# Parses the serialized fields of a single-document Unity asset into a hashtable.
# Scalar values stay raw strings; lists become arrays of hashtables (block items)
# or hashtables with _flow (inline {...} items).
function Parse-Asset([string]$path) {
    $lines = [IO.File]::ReadAllLines($path, [Text.Encoding]::UTF8)
    $data = @{}
    $i = 0
    while ($i -lt $lines.Count -and $lines[$i] -notmatch '^  m_EditorClassIdentifier') { $i++ }
    if ($i -ge $lines.Count) { return $null }
    $data['_class'] = ($lines[$i] -replace '^.*::', '').Trim()
    $i++
    while ($i -lt $lines.Count) {
        $line = $lines[$i]
        if ($line -match '^  (\w+):\s*(.*)$') {
            $key = $Matches[1]; $val = $Matches[2].TrimEnd()
            $i++
            if ($val -eq '') {
                $list = New-Object System.Collections.ArrayList
                while ($i -lt $lines.Count -and $lines[$i] -match '^  - ') {
                    $itemRest = $lines[$i].Substring(4)
                    $i++
                    if ($itemRest.StartsWith('{')) {
                        [void]$list.Add(@{ _flow = $itemRest })
                    }
                    else {
                        $item = @{}
                        if ($itemRest -match '^(\w+):\s*(.*)$') { $item[$Matches[1]] = $Matches[2].TrimEnd() }
                        while ($i -lt $lines.Count -and $lines[$i] -match '^    (\w+):\s*(.*)$') {
                            $item[$Matches[1]] = $Matches[2].TrimEnd(); $i++
                        }
                        [void]$list.Add($item)
                    }
                }
                $data[$key] = $list
            }
            elseif ($val -eq '[]') {
                $data[$key] = @()
            }
            else {
                # scalar continuation: wrapped long strings are indented 4+ spaces
                while ($i -lt $lines.Count -and $lines[$i] -match '^    \S' ) {
                    $cont = $lines[$i].Trim()
                    if ($val.EndsWith('\') -and -not $val.EndsWith('\\')) {
                        $val = $val.Substring(0, $val.Length - 1) + $cont
                    } else {
                        $val = $val + ' ' + $cont
                    }
                    $i++
                }
                $data[$key] = $val
            }
        }
        else { $i++ }
    }
    return $data
}

function G($d, $k, $def = '') {
    if ($d.ContainsKey($k) -and $null -ne $d[$k]) { return $d[$k] } else { return $def }
}

function RefGuid([string]$v) {
    if ($v -match 'guid: ([0-9a-f]{32})') { return $Matches[1] }
    return ''   # {fileID: 0} or malformed -> empty
}

function NumOr($d, $k, [double]$def) {
    $raw = G $d $k ''
    if ($raw -eq '') { return $def }
    $out = 0.0
    if ([double]::TryParse($raw, [Globalization.NumberStyles]::Float, $inv, [ref]$out)) { return $out }
    return $def
}

function YN([string]$v) { if ($v -eq '1') { 'yes' } else { 'no' } }

function ColorFmt([string]$v) {
    if ($v -eq '') { return '' }
    return ($v -replace '[{}]', '' -replace ':\s*', '=' -replace ',\s*', ' ').Trim()
}

# ---------- CSV writer (Excel-safe) ----------
function CsvEscape($f) {
    $s = [string]$f
    if ($s -match '[",\r\n]') { return '"' + $s.Replace('"', '""') + '"' }
    return $s
}
function Write-Csv([string]$path, [string[]]$header, $rows) {
    $out = New-Object System.Collections.Generic.List[string]
    $out.Add('sep=,')
    $out.Add((($header | ForEach-Object { CsvEscape $_ }) -join ','))
    foreach ($r in $rows) {
        $out.Add((($r | ForEach-Object { CsvEscape $_ }) -join ','))
    }
    [IO.File]::WriteAllLines($path, $out, (New-Object System.Text.UTF8Encoding($true)))
    Write-Host ("{0}: {1} rows" -f (Split-Path $path -Leaf), $rows.Count)
}

# ---------- guid -> filename map from .meta files ----------
$metaMap = @{}
Get-ChildItem $gd -Recurse -Filter *.meta | ForEach-Object {
    $txt = [IO.File]::ReadAllText($_.FullName)
    if ($txt -match 'guid: ([0-9a-f]{32})') {
        $base = $_.Name -replace '\.meta$', '' -replace '\.asset$', '' -replace '\.png$', ''
        $metaMap[$Matches[1]] = $base
    }
}

function OwnGuid([string]$assetPath) {
    $meta = "$assetPath.meta"
    if (Test-Path $meta) {
        $txt = [IO.File]::ReadAllText($meta)
        if ($txt -match 'guid: ([0-9a-f]{32})') { return $Matches[1] }
    }
    return ''
}

# ---------- parse all content groups ----------
function Load-Group([string]$sub, [string]$class) {
    $items = New-Object System.Collections.ArrayList
    $dir = Join-Path $gd $sub
    foreach ($f in (Get-ChildItem $dir -Filter *.asset | Sort-Object Name)) {
        $d = Parse-Asset $f.FullName
        if ($null -eq $d -or $d['_class'] -ne $class) { continue }
        $d['_guid'] = OwnGuid $f.FullName
        $d['_file'] = $f.Name
        [void]$items.Add($d)
    }
    return $items
}

$pieces    = Load-Group 'Pieces'    'Proto.PieceDefinition'
$statuses  = Load-Group 'Statuses'  'Proto.StatusDefinition'
$buffs     = Load-Group 'Buffs'     'Proto.BuffDefinition'
$reactions = Load-Group 'Reactions' 'Proto.ReactionDefinition'
$recipes   = Load-Group 'Recipes'   'Proto.RecipeDefinition'
$enemies   = Load-Group 'Enemies'   'Proto.EnemyArchetype'
$heroes    = Load-Group 'Heroes'    'Proto.HeroLoadout'

# guid -> display name (pieces, statuses, buffs), plus piece lookup for recipes
$nameByGuid  = @{}
$pieceByGuid = @{}
foreach ($p in $pieces)   { if ($p['_guid']) { $nameByGuid[$p['_guid']] = Unquote (G $p 'DisplayName' $p['_file']); $pieceByGuid[$p['_guid']] = $p } }
foreach ($s in $statuses) { if ($s['_guid']) { $nameByGuid[$s['_guid']] = Unquote (G $s 'DisplayName' $s['_file']) } }
foreach ($b in $buffs)    { if ($b['_guid']) { $nameByGuid[$b['_guid']] = Unquote (G $b 'DisplayName' $b['_file']) } }

function ResolveRef([string]$v) {
    $g = RefGuid $v
    if ($g -eq '') { return '' }
    if ($nameByGuid.ContainsKey($g)) { return $nameByGuid[$g] }
    if ($metaMap.ContainsKey($g))    { return $metaMap[$g] }
    return $g
}

function FlattenMods($list) {
    if ($null -eq $list -or $list.Count -eq 0) { return '' }
    $parts = foreach ($m in $list) {
        $name = EN $StatKindNames (G $m 'Type' '0')
        $v = [string](G $m 'Value' '0')
        if ($v.StartsWith('-')) { "$name$v" } else { "$name+$v" }
    }
    return ($parts -join '; ')
}

# ---------- pieces.csv ----------
$rows = New-Object System.Collections.ArrayList
foreach ($p in $pieces) {
    $id     = Unquote (G $p 'Id')
    $kindIx = [int](G $p 'Kind' '0')
    $layer  = if ($kindIx -eq 7) { 'Segel' } else { EN $LayerNames (G $p 'Layer' '1') }
    $dmg    = NumOr $p 'BaseDamage' 0
    $cd     = NumOr $p 'BaseCooldown' 1
    $mana   = NumOr $p 'ManaCost' 0
    $dps    = ''
    if ($cd -ne 0 -and $dmg -ne 0) { $dps = ([math]::Round($dmg / $cd, 2)).ToString($inv) }
    $mps    = ''
    if ($cd -ne 0) { $mps = ([math]::Round($mana / $cd, 2)).ToString($inv) }
    $aura   = EN $AuraNames (G $p 'Aura' '0')
    if ($aura -eq 'None') { $aura = '' }
    $icon   = if (Test-Path (Join-Path $gd "Icons\Icon_$id.png")) { 'yes' } else { 'no' }
    [void]$rows.Add(@(
        $id,
        (Unquote (G $p 'DisplayName')),
        $layer,
        (EN $CastKindNames (G $p 'Kind' '0')),
        (EN $ElementNames (G $p 'Element' '3')),
        (G $p 'Stars' '1'),
        (EN $ShapeNames (G $p 'Shape' '1')),
        (G $p 'BaseDamage' '0'),
        (G $p 'BaseCooldown' '1'),
        (G $p 'Range' '0'),
        (G $p 'Radius' '0'),
        (G $p 'ManaCost' '0'),
        (G $p 'Hits' '1'),
        (G $p 'Forks' '1'),
        (G $p 'Bounces' '0'),
        (G $p 'BounceRange' '6'),
        (ResolveRef (G $p 'AppliedStatus')),
        (G $p 'StatusDuration' '0'),
        (G $p 'AppliedPoints' '1'),
        $aura,
        (G $p 'AuraValue' '0'),
        (G $p 'ElementMatchBonus' '0'),
        (FlattenMods (G $p 'Stats' @())),
        (EN $TriggerNames (G $p 'Trigger' '0')),
        (ResolveRef (G $p 'TriggerStatus')),
        (G $p 'TriggerPoints' '10'),
        (YN (G $p 'ConsumeTriggerPoints' '1')),
        (G $p 'MaxDetonations' '8'),
        (G $p 'ZoneDuration' '4'),
        (G $p 'ZoneTickInterval' '0.5'),
        (G $p 'ZoneDrift' '0'),
        (G $p 'TelegraphDelay' '0.9'),
        (G $p 'PushForce' '12'),
        (G $p 'LiftDuration' '1.6'),
        (G $p 'TravelSpeed' '9'),
        (ResolveRef (G $p 'GrantOnCast')),
        (ResolveRef (G $p 'GrantOnKill')),
        (ResolveRef (G $p 'ConsumesCharge')),
        (G $p 'DamagePerCharge' '0.35'),
        $icon,
        (Unquote (G $p 'Blurb')),
        $dps,
        $mps
    ))
}
Write-Csv (Join-Path $outDir 'pieces.csv') @(
    'Id','Nama','Layer','Kind','Element','Bintang','Bentuk','Damage','Cooldown','Range','Radius',
    'Mana','Hits','Forks','Bounces','BounceRange','Status','DurasiStatus','Poin','Aura','AuraValue',
    'ElementMatchBonus','Stats','Trigger','TriggerStatus','TriggerPoints','KonsumsiPoinTrigger',
    'MaxDetonations','ZoneDuration','ZoneTickInterval','ZoneDrift','TelegraphDelay','PushForce',
    'LiftDuration','TravelSpeed','GrantOnCast','GrantOnKill','ConsumesCharge','DamagePerCharge',
    'PunyaIkon','Blurb','DPS','ManaPerDetik'
) $rows

# ---------- recipes.csv ----------
$rows = New-Object System.Collections.ArrayList
foreach ($r in $recipes) {
    $ing = @(G $r 'Ingredients' @())
    $names = @('', '', '')
    $count = 0
    $cells = 0
    for ($k = 0; $k -lt $ing.Count -and $k -lt 3; $k++) {
        $flow = [string]$ing[$k]['_flow']
        $g = RefGuid $flow
        if ($g -eq '') { continue }
        $count++
        $names[$k] = ResolveRef $flow
        if ($pieceByGuid.ContainsKey($g)) {
            $shapeIx = [int](G $pieceByGuid[$g] 'Shape' '1')
            if ($shapeIx -ge 0 -and $shapeIx -lt $ShapeCells.Count) { $cells += $ShapeCells[$shapeIx] }
        }
    }
    $resFlow = G $r 'Result'
    $resGuid = RefGuid $resFlow
    $resStars = ''
    if ($pieceByGuid.ContainsKey($resGuid)) { $resStars = G $pieceByGuid[$resGuid] 'Stars' '1' }
    [void]$rows.Add(@(
        (ResolveRef $resFlow), $resStars, $names[0], $names[1], $names[2], $count, $cells
    ))
}
Write-Csv (Join-Path $outDir 'recipes.csv') @(
    'Hasil','HasilBintang','Bahan1','Bahan2','Bahan3','JumlahBahan','TotalPetakBahan'
) $rows

# ---------- statuses.csv ----------
$rows = New-Object System.Collections.ArrayList
foreach ($s in $statuses) {
    [void]$rows.Add(@(
        (Unquote (G $s 'Id')),
        (Unquote (G $s 'DisplayName')),
        (G $s 'MaxPoints' '1'),
        (G $s 'TickInterval' '0.5'),
        (G $s 'DamagePerTickPerPoint' '0'),
        (G $s 'MoveSpeedMultiplier' '1'),
        (G $s 'DamageTakenMultiplier' '1'),
        (G $s 'PullStrength' '0'),
        (Unquote (G $s 'Blurb'))
    ))
}
Write-Csv (Join-Path $outDir 'statuses.csv') @(
    'Id','Nama','MaxPoints','TickInterval','DamagePerTickPerPoint','MoveSpeedMultiplier',
    'DamageTakenMultiplier','PullStrength','Blurb'
) $rows

# ---------- buffs.csv ----------
$rows = New-Object System.Collections.ArrayList
foreach ($b in $buffs) {
    [void]$rows.Add(@(
        (Unquote (G $b 'Id')),
        (Unquote (G $b 'DisplayName')),
        (YN (G $b 'IsDebuff' '0')),
        (G $b 'Duration' '6'),
        (FlattenMods (G $b 'Mods' @())),
        (Unquote (G $b 'Blurb'))
    ))
}
Write-Csv (Join-Path $outDir 'buffs.csv') @('Id','Nama','Kutukan','Durasi','Mods','Blurb') $rows

# ---------- reactions.csv ----------
$rows = New-Object System.Collections.ArrayList
foreach ($x in $reactions) {
    [void]$rows.Add(@(
        (Unquote (G $x 'DisplayName')),
        (ResolveRef (G $x 'A')),
        (ResolveRef (G $x 'B')),
        (G $x 'MinPointsA' '1'),
        (G $x 'MinPointsB' '1'),
        (YN (G $x 'ConsumeA' '1')),
        (YN (G $x 'ConsumeB' '1')),
        (G $x 'BurstDamage' '0'),
        (G $x 'BurstDamagePerPointA' '0'),
        (G $x 'BurstPctOfMaxHp' '0'),
        (G $x 'BurstRadius' '3'),
        (ResolveRef (G $x 'ApplyStatus')),
        (G $x 'ApplyPoints' '1'),
        (YN (G $x 'SpreadToNearby' '0')),
        (ResolveRef (G $x 'GrantBuff'))
    ))
}
Write-Csv (Join-Path $outDir 'reactions.csv') @(
    'Nama','StatusA','StatusB','MinPoinA','MinPoinB','KonsumsiA','KonsumsiB','BurstDamage',
    'BurstPerPoinA','BurstPctOfMaxHp','BurstRadius','StatusHasil','PoinHasil','Menular','BuffHadiah'
) $rows

# ---------- enemies.csv ----------
$rows = New-Object System.Collections.ArrayList
foreach ($e in $enemies) {
    [void]$rows.Add(@(
        (Unquote (G $e 'Id')),
        (Unquote (G $e 'DisplayName')),
        (G $e 'FromWave' '1'),
        (G $e 'Weight' '1'),
        (G $e 'WeightPerWave' '0'),
        (G $e 'WeightMax' '20'),
        (G $e 'HpMultiplier' '1'),
        (G $e 'SpeedMultiplier' '1'),
        (G $e 'Scale' '1'),
        (G $e 'HoverHeight' '0'),
        (YN (G $e 'UseTint' '0')),
        (ColorFmt (G $e 'Tint')),
        (G $e 'PreferredRange' '0'),
        (YN (G $e 'Flanks' '1')),
        (G $e 'AttackInterval' '0'),
        (G $e 'AttackDamage' '8'),
        (G $e 'ShotSpeed' '11'),
        (ColorFmt (G $e 'ShotColor')),
        (ResolveRef (G $e 'Curse')),
        (Unquote (G $e 'Blurb'))
    ))
}
Write-Csv (Join-Path $outDir 'enemies.csv') @(
    'Id','Nama','FromWave','Weight','WeightPerWave','WeightMax','HpMultiplier','SpeedMultiplier',
    'Scale','HoverHeight','UseTint','Tint','PreferredRange','Flanks','AttackInterval',
    'AttackDamage','ShotSpeed','ShotColor','Curse','Blurb'
) $rows

# ---------- heroes.csv ----------
$rows = New-Object System.Collections.ArrayList
foreach ($h in $heroes) {
    $placed = @(G $h 'Placed' @())
    $placedParts = foreach ($seat in $placed) {
        $pn = ResolveRef ([string](G $seat 'Piece'))
        $ox = ''; $oy = ''
        if ([string](G $seat 'Origin') -match 'x:\s*(-?\d+),\s*y:\s*(-?\d+)') { $ox = $Matches[1]; $oy = $Matches[2] }
        $rot = [string](G $seat 'Rot' '0')
        "$pn@($ox,$oy) rot$rot"
    }
    $loose = @(G $h 'Loose' @())
    $looseParts = foreach ($l in $loose) { ResolveRef ([string]$l['_flow']) }
    [void]$rows.Add(@(
        (Unquote (G $h 'Id')),
        (Unquote (G $h 'DisplayName')),
        (Unquote (G $h 'Blurb')),
        $placed.Count,
        (($placedParts | Where-Object { $_ }) -join '; '),
        $loose.Count,
        (($looseParts | Where-Object { $_ }) -join '; ')
    ))
}
Write-Csv (Join-Path $outDir 'heroes.csv') @(
    'Id','Nama','Blurb','JumlahTerpasang','Terpasang','JumlahLepas','Lepas'
) $rows

Write-Host 'Done.'
