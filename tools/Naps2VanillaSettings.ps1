#requires -Version 5.1
# NAPS2 vanilla settings backup/restore. No administrator rights required.
# F12 cancels operations only while this console window has keyboard focus.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName System.IO.Compression

$SourceFolder = Join-Path $env:APPDATA 'NAPS2'
$ArchiveFile = 'D:\Rezerv\Naps2Vanil.zip'

function Check-F12 {
    try {
        if ([Console]::KeyAvailable) {
            $key = [Console]::ReadKey($true)
            if ($key.Key -eq [ConsoleKey]::F12) {
                throw [OperationCanceledException]::new('Операция остановлена клавишей F12.')
            }
        }
    } catch [InvalidOperationException] {
        # No interactive console (for example, a redirected host).
    }
}

function Read-Confirmation([string] $PromptText) {
    Write-Host -NoNewline $PromptText
    $answer = [Text.StringBuilder]::new()
    while ($true) {
        $key = [Console]::ReadKey($true)
        if ($key.Key -eq [ConsoleKey]::F12) {
            throw [OperationCanceledException]::new('Операция остановлена клавишей F12.')
        }
        if ($key.Key -eq [ConsoleKey]::Escape) {
            Write-Host ''
            return ''
        }
        if ($key.Key -eq [ConsoleKey]::Enter) {
            Write-Host ''
            return $answer.ToString()
        }
        if ($key.Key -eq [ConsoleKey]::Backspace) {
            if ($answer.Length -gt 0) {
                $answer.Length -= 1
                [Console]::Write(([string][char]8) + ' ' + ([string][char]8))
            }
        } elseif (-not [char]::IsControl($key.KeyChar)) {
            [void]$answer.Append($key.KeyChar)
            [Console]::Write($key.KeyChar)
        }
    }
}

function Assert-Closed {
    $running = @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -match '^NAPS2($|\.)' })
    if ($running.Count -gt 0) {
        throw 'Сначала закройте NAPS2, включая Portable и фоновые процессы.'
    }
}

function Copy-StreamWithF12 {
    param(
        [Parameter(Mandatory)] [System.IO.Stream] $From,
        [Parameter(Mandatory)] [System.IO.Stream] $To
    )
    $buffer = [byte[]]::new(1048576)
    while (($size = $From.Read($buffer, 0, $buffer.Length)) -gt 0) {
        Check-F12
        $To.Write($buffer, 0, $size)
    }
}

function Save-VanillaSettings {
    Assert-Closed
    if (-not (Test-Path -LiteralPath $SourceFolder -PathType Container)) {
        throw "Папка настроек не найдена: $SourceFolder"
    }
    if (-not (Test-Path -LiteralPath 'D:\' -PathType Container)) {
        throw 'Диск D: недоступен. Архив не создавался.'
    }
    $destFolder = Split-Path -Parent $ArchiveFile
    [void][IO.Directory]::CreateDirectory($destFolder)

    if (Test-Path -LiteralPath $ArchiveFile) {
        Write-Host 'Архив уже существует. Его замена требует подтверждения.'
        $answer = Read-Confirmation 'Для замены введите ПЕРЕЗАПИСАТЬ: '
        if ($answer -ine 'ПЕРЕЗАПИСАТЬ') {
            Write-Host 'Отмена. Существующий архив сохранён.'
            return
        }
    }

    $temp = Join-Path $destFolder ('.Naps2Vanil-' + [guid]::NewGuid().ToString('N') + '.zip')
    $stream = $null
    $archive = $null
    try {
        $stream = [IO.File]::Open($temp, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
        $archive = [IO.Compression.ZipArchive]::new(
            $stream, [IO.Compression.ZipArchiveMode]::Create, $true,
            [Text.Encoding]::UTF8)
        [void]$archive.CreateEntry('NAPS2/')
        $directories = @(Get-ChildItem -LiteralPath $SourceFolder -Directory -Force -Recurse)
        foreach ($dir in $directories) {
            Check-F12
            $relative = $dir.FullName.Substring($SourceFolder.Length).TrimStart([char[]]@('\','/'))
            [void]$archive.CreateEntry(('NAPS2/' + $relative.Replace('\','/') + '/'))
        }

        $files = @(Get-ChildItem -LiteralPath $SourceFolder -File -Force -Recurse)
        $i = 0
        foreach ($file in $files) {
            Check-F12
            $relative = $file.FullName.Substring($SourceFolder.Length).TrimStart([char[]]@('\','/'))
            $entry = $archive.CreateEntry('NAPS2/' + $relative.Replace('\','/'),
                [IO.Compression.CompressionLevel]::Optimal)
            $src = $null
            $dst = $null
            try {
                $src = [IO.File]::Open($file.FullName, [IO.FileMode]::Open,
                    [IO.FileAccess]::Read, [IO.FileShare]::Read)
                $dst = $entry.Open()
                Copy-StreamWithF12 -From $src -To $dst
            } finally {
                if ($dst) { $dst.Dispose() }
                if ($src) { $src.Dispose() }
            }
            $i++
            if ($i % 25 -eq 0) { Write-Host "Упаковано файлов: $i / $($files.Count)" }
        }
        $archive.Dispose()
        $archive = $null
        $stream.Dispose()
        $stream = $null
        Check-F12

        # Keep an older backup, rather than destroying the only rollback copy.
        $previous = $null
        if (Test-Path -LiteralPath $ArchiveFile) {
            $previous = Join-Path $destFolder (
                'Naps2Vanil-previous-' + (Get-Date -Format 'yyyyMMdd-HHmmss') +
                '-' + [guid]::NewGuid().ToString('N').Substring(0,6) + '.zip')
            Move-Item -LiteralPath $ArchiveFile -Destination $previous
        }
        try {
            Move-Item -LiteralPath $temp -Destination $ArchiveFile
        } catch {
            if ($previous -and -not (Test-Path -LiteralPath $ArchiveFile)) {
                Move-Item -LiteralPath $previous -Destination $ArchiveFile
            }
            throw
        }
        Write-Host "Настройки сохранены в $ArchiveFile"
        if ($previous) { Write-Host "Предыдущий архив сохранён отдельно: $previous" }
    } finally {
        if ($archive) { $archive.Dispose() }
        if ($stream) { $stream.Dispose() }
        if (Test-Path -LiteralPath $temp) {
            Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
        }
    }
}

function Restore-VanillaSettings {
    Assert-Closed
    if (-not (Test-Path -LiteralPath $ArchiveFile -PathType Leaf)) {
        throw "Архив отсутствует: $ArchiveFile"
    }
    Write-Host 'Текущие настройки NAPS2 будут заменены содержимым архива.'
    Write-Host 'Старые настройки сохранятся в отдельной папке на случай отката.'
    $answer = Read-Confirmation 'Для продолжения введите ВОССТАНОВИТЬ: '
    if ($answer -ine 'ВОССТАНОВИТЬ') {
        Write-Host 'Восстановление отменено.'
        return
    }
    $appData = Split-Path -Parent $SourceFolder
    $stage = Join-Path $appData ('NAPS2-restore-stage-' + [guid]::NewGuid().ToString('N'))
    $stageRoot = Join-Path $stage 'NAPS2'
    $zipStream = $null
    $zip = $null
    try {
        [void][IO.Directory]::CreateDirectory($stageRoot)
        $zipStream = [IO.File]::Open($ArchiveFile, [IO.FileMode]::Open,
            [IO.FileAccess]::Read, [IO.FileShare]::Read)
        $zip = [IO.Compression.ZipArchive]::new(
            $zipStream, [IO.Compression.ZipArchiveMode]::Read, $true,
            [Text.Encoding]::UTF8)
        foreach ($entry in $zip.Entries) {
            Check-F12
            $name = $entry.FullName.Replace('\','/')
            if (-not $name.StartsWith('NAPS2/', [StringComparison]::OrdinalIgnoreCase)) {
                throw "Архив содержит неожиданный путь: $name"
            }
            $relative = $name.Substring(6)
            if (-not $relative) { continue }
            $parts = $relative.TrimEnd('/').Split('/')
            if (@($parts | Where-Object { $_ -eq '' -or $_ -eq '.' -or $_ -eq '..' -or $_.Contains(':') }).Count) {
                throw "Недопустимый путь внутри архива: $name"
            }
            $target = [IO.Path]::GetFullPath((Join-Path $stageRoot ($relative.Replace('/','\'))))
            $prefix = [IO.Path]::GetFullPath($stageRoot).TrimEnd('\') + '\'
            if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Недопустимый путь внутри архива: $name"
            }
            if ($name.EndsWith('/')) {
                [void][IO.Directory]::CreateDirectory($target)
            } else {
                [void][IO.Directory]::CreateDirectory((Split-Path -Parent $target))
                $src = $null
                $dst = $null
                try {
                    $src = $entry.Open()
                    $dst = [IO.File]::Open($target, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
                    Copy-StreamWithF12 -From $src -To $dst
                } finally {
                    if ($dst) { $dst.Dispose() }
                    if ($src) { $src.Dispose() }
                }
            }
        }
        $zip.Dispose()
        $zip = $null
        $zipStream.Dispose()
        $zipStream = $null
        Check-F12

        $oldPath = $null
        if (Test-Path -LiteralPath $SourceFolder) {
            $oldPath = Join-Path $appData (
                'NAPS2-before-restore-' + (Get-Date -Format 'yyyyMMdd-HHmmss') +
                '-' + [guid]::NewGuid().ToString('N').Substring(0,6))
            Move-Item -LiteralPath $SourceFolder -Destination $oldPath
        }
        try {
            Move-Item -LiteralPath $stageRoot -Destination $SourceFolder
        } catch {
            if ($oldPath -and -not (Test-Path -LiteralPath $SourceFolder)) {
                Move-Item -LiteralPath $oldPath -Destination $SourceFolder
            }
            throw
        }
        Write-Host "Настройки восстановлены в $SourceFolder"
        if ($oldPath) { Write-Host "Предыдущие настройки сохранены в $oldPath" }
    } finally {
        if ($zip) { $zip.Dispose() }
        if ($zipStream) { $zipStream.Dispose() }
        if (Test-Path -LiteralPath $stage) {
            Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

while ($true) {
    Write-Host ''
    Write-Host '==============================================='
    Write-Host '  NAPS2: резервирование настроек ванильной версии'
    Write-Host '==============================================='
    Write-Host "Архив: $ArchiveFile"
    Write-Host '1 — Сохранить настройки в ZIP'
    Write-Host '2 — Восстановить настройки из ZIP'
    Write-Host '0 / Esc / F12 — Выход'
    $key = [Console]::ReadKey($true).Key
    if ($key -in @([ConsoleKey]::F12, [ConsoleKey]::Escape,
                   [ConsoleKey]::D0, [ConsoleKey]::NumPad0)) { break }
    try {
        switch ($key) {
            { $_ -in @([ConsoleKey]::D1, [ConsoleKey]::NumPad1) } { Save-VanillaSettings; break }
            { $_ -in @([ConsoleKey]::D2, [ConsoleKey]::NumPad2) } { Restore-VanillaSettings; break }
            default { Write-Host 'Выберите 1, 2 или 0.' }
        }
    } catch [OperationCanceledException] {
        Write-Host 'Операция остановлена клавишей F12; исходные настройки сохранены.'
    } catch {
        Write-Host ('Ошибка: ' + $_.Exception.Message)
    }
}
