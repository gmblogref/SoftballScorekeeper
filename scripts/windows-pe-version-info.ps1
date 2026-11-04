function Get-WindowsPeVersionMetadata {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
        throw "Windows PE file was not found at '$resolvedPath'."
    }

    $bytes = [System.IO.File]::ReadAllBytes($resolvedPath)

    function Assert-ByteRange {
        param([int64]$Offset, [int64]$Length, [string]$Description)

        if ($Offset -lt 0 -or $Length -lt 0 -or $Offset + $Length -gt $bytes.LongLength) {
            throw "$Description extends outside the Windows PE file."
        }
    }

    function Read-UInt16 {
        param([int64]$Offset, [string]$Description)

        Assert-ByteRange -Offset $Offset -Length 2 -Description $Description
        return [System.BitConverter]::ToUInt16($bytes, [int]$Offset)
    }

    function Read-UInt32 {
        param([int64]$Offset, [string]$Description)

        Assert-ByteRange -Offset $Offset -Length 4 -Description $Description
        return [System.BitConverter]::ToUInt32($bytes, [int]$Offset)
    }

    function Convert-RvaToFileOffset {
        param([uint32]$Rva, [object[]]$Sections, [string]$Description)

        foreach ($section in $Sections) {
            $sectionLength = [Math]::Max([uint64]$section.VirtualSize, [uint64]$section.RawSize)
            $sectionEnd = [uint64]$section.VirtualAddress + $sectionLength
            if ([uint64]$Rva -ge [uint64]$section.VirtualAddress -and [uint64]$Rva -lt $sectionEnd) {
                $sectionRelativeOffset = [uint64]$Rva - [uint64]$section.VirtualAddress
                if ($sectionRelativeOffset -ge [uint64]$section.RawSize) {
                    throw "$Description maps to unbacked virtual section data."
                }
                $offset = [uint64]$section.RawOffset + $sectionRelativeOffset
                Assert-ByteRange -Offset $offset -Length 1 -Description $Description
                return [int64]$offset
            }
        }

        throw "$Description does not map to a Windows PE section."
    }

    function Get-ResourceDirectoryEntries {
        param([uint32]$RelativeOffset, [int64]$ResourceBaseOffset)

        if ([uint64]$RelativeOffset + 16 -gt [uint64]$resourceSize) {
            throw 'Windows PE resource directory extends outside the declared resource table.'
        }
        $directoryOffset = $ResourceBaseOffset + [uint64]$RelativeOffset
        Assert-ByteRange -Offset $directoryOffset -Length 16 -Description 'Windows PE resource directory'
        $namedCount = Read-UInt16 -Offset ($directoryOffset + 12) -Description 'Windows PE named-resource count'
        $idCount = Read-UInt16 -Offset ($directoryOffset + 14) -Description 'Windows PE ID-resource count'
        $entryCount = [uint32]$namedCount + [uint32]$idCount
        if ([uint64]$RelativeOffset + 16 + ([uint64]$entryCount * 8) -gt [uint64]$resourceSize) {
            throw 'Windows PE resource entries extend outside the declared resource table.'
        }
        Assert-ByteRange -Offset ($directoryOffset + 16) -Length ([uint64]$entryCount * 8) -Description 'Windows PE resource entries'

        return @(
            for ($index = 0; $index -lt $entryCount; $index++) {
                $entryOffset = $directoryOffset + 16 + ($index * 8)
                $name = Read-UInt32 -Offset $entryOffset -Description 'Windows PE resource name'
                $data = Read-UInt32 -Offset ($entryOffset + 4) -Description 'Windows PE resource data offset'
                [pscustomobject]@{
                    IsNamed = ($name -band 0x80000000) -ne 0
                    Id = $name -band 0x0000FFFF
                    IsDirectory = ($data -band 0x80000000) -ne 0
                    RelativeOffset = $data -band 0x7FFFFFFF
                }
            }
        )
    }

    Assert-ByteRange -Offset 0 -Length 64 -Description 'Windows PE DOS header'
    if ((Read-UInt16 -Offset 0 -Description 'Windows PE DOS signature') -ne 0x5A4D) {
        throw 'Windows executable does not contain an MZ signature.'
    }

    $peOffset = Read-UInt32 -Offset 0x3C -Description 'Windows PE header offset'
    Assert-ByteRange -Offset $peOffset -Length 24 -Description 'Windows PE header'
    if ((Read-UInt32 -Offset $peOffset -Description 'Windows PE signature') -ne 0x00004550) {
        throw 'Windows executable does not contain a PE signature.'
    }

    $sectionCount = Read-UInt16 -Offset ($peOffset + 6) -Description 'Windows PE section count'
    $optionalHeaderSize = Read-UInt16 -Offset ($peOffset + 20) -Description 'Windows PE optional-header size'
    $optionalHeaderOffset = [uint64]$peOffset + 24
    Assert-ByteRange -Offset $optionalHeaderOffset -Length $optionalHeaderSize -Description 'Windows PE optional header'
    $optionalMagic = Read-UInt16 -Offset $optionalHeaderOffset -Description 'Windows PE optional-header magic'
    if ($optionalMagic -eq 0x10B) {
        $resourceDirectoryOffset = $optionalHeaderOffset + 112
    }
    elseif ($optionalMagic -eq 0x20B) {
        $resourceDirectoryOffset = $optionalHeaderOffset + 128
    }
    else {
        throw "Windows executable contains unsupported optional-header magic 0x$($optionalMagic.ToString('X4'))."
    }

    if ($resourceDirectoryOffset + 8 -gt $optionalHeaderOffset + $optionalHeaderSize) {
        throw 'Windows executable optional header does not contain a resource-table directory.'
    }
    $resourceRva = Read-UInt32 -Offset $resourceDirectoryOffset -Description 'Windows PE resource-table RVA'
    $resourceSize = Read-UInt32 -Offset ($resourceDirectoryOffset + 4) -Description 'Windows PE resource-table size'
    if ($resourceRva -eq 0 -or $resourceSize -eq 0) {
        throw 'Windows executable does not contain a resource table.'
    }

    $sectionHeadersOffset = $optionalHeaderOffset + $optionalHeaderSize
    Assert-ByteRange -Offset $sectionHeadersOffset -Length ([uint64]$sectionCount * 40) -Description 'Windows PE section headers'
    $sections = @(
        for ($index = 0; $index -lt $sectionCount; $index++) {
            $sectionOffset = $sectionHeadersOffset + ($index * 40)
            [pscustomobject]@{
                VirtualSize = Read-UInt32 -Offset ($sectionOffset + 8) -Description 'Windows PE section virtual size'
                VirtualAddress = Read-UInt32 -Offset ($sectionOffset + 12) -Description 'Windows PE section RVA'
                RawSize = Read-UInt32 -Offset ($sectionOffset + 16) -Description 'Windows PE section raw size'
                RawOffset = Read-UInt32 -Offset ($sectionOffset + 20) -Description 'Windows PE section raw offset'
            }
        }
    )
    $resourceBaseOffset = Convert-RvaToFileOffset -Rva $resourceRva -Sections $sections -Description 'Windows PE resource table'

    $rootEntries = Get-ResourceDirectoryEntries -RelativeOffset 0 -ResourceBaseOffset $resourceBaseOffset
    $versionTypeEntries = @($rootEntries | Where-Object { -not $_.IsNamed -and $_.Id -eq 16 })
    if ($versionTypeEntries.Count -ne 1 -or -not $versionTypeEntries[0].IsDirectory) {
        throw "Windows executable must contain exactly one RT_VERSION resource directory; found $($versionTypeEntries.Count)."
    }

    $versionDataEntries = New-Object System.Collections.Generic.List[object]
    $nameEntries = Get-ResourceDirectoryEntries -RelativeOffset $versionTypeEntries[0].RelativeOffset -ResourceBaseOffset $resourceBaseOffset
    foreach ($nameEntry in $nameEntries) {
        if (-not $nameEntry.IsDirectory) {
            throw 'Windows RT_VERSION name entry does not reference a language directory.'
        }
        $languageEntries = Get-ResourceDirectoryEntries -RelativeOffset $nameEntry.RelativeOffset -ResourceBaseOffset $resourceBaseOffset
        foreach ($languageEntry in $languageEntries) {
            if ($languageEntry.IsDirectory) {
                throw 'Windows RT_VERSION language entry does not reference version data.'
            }
            $versionDataEntries.Add($languageEntry)
        }
    }
    if ($versionDataEntries.Count -ne 1) {
        throw "Windows executable must contain exactly one RT_VERSION data entry; found $($versionDataEntries.Count)."
    }

    $dataEntryOffset = $resourceBaseOffset + [uint64]$versionDataEntries[0].RelativeOffset
    if ([uint64]$versionDataEntries[0].RelativeOffset + 16 -gt [uint64]$resourceSize) {
        throw 'Windows RT_VERSION data entry extends outside the declared resource table.'
    }
    Assert-ByteRange -Offset $dataEntryOffset -Length 16 -Description 'Windows RT_VERSION data entry'
    $versionRva = Read-UInt32 -Offset $dataEntryOffset -Description 'Windows RT_VERSION data RVA'
    $versionSize = Read-UInt32 -Offset ($dataEntryOffset + 4) -Description 'Windows RT_VERSION data size'
    if ($versionSize -lt 92) {
        throw 'Windows RT_VERSION resource is too small to contain VS_VERSION_INFO.'
    }
    $versionOffset = Convert-RvaToFileOffset -Rva $versionRva -Sections $sections -Description 'Windows RT_VERSION data'
    Assert-ByteRange -Offset $versionOffset -Length $versionSize -Description 'Windows RT_VERSION data'

    $versionLength = Read-UInt16 -Offset $versionOffset -Description 'VS_VERSION_INFO length'
    $valueLength = Read-UInt16 -Offset ($versionOffset + 2) -Description 'VS_VERSION_INFO value length'
    $valueType = Read-UInt16 -Offset ($versionOffset + 4) -Description 'VS_VERSION_INFO value type'
    if ($versionLength -gt $versionSize -or $versionLength -lt 92 -or $valueLength -ne 52 -or $valueType -ne 0) {
        throw 'Windows RT_VERSION resource contains malformed VS_VERSION_INFO lengths or type.'
    }

    $keyCharacters = New-Object System.Collections.Generic.List[char]
    $cursor = [uint64]$versionOffset + 6
    while ($true) {
        $character = Read-UInt16 -Offset $cursor -Description 'VS_VERSION_INFO key'
        $cursor += 2
        if ($character -eq 0) {
            break
        }
        if ($keyCharacters.Count -ge 64) {
            throw 'Windows RT_VERSION resource contains an invalid VS_VERSION_INFO key.'
        }
        $keyCharacters.Add([char]$character)
    }
    $versionKey = -join $keyCharacters
    if ($versionKey -cne 'VS_VERSION_INFO') {
        throw "Windows RT_VERSION resource contains unexpected root key '$versionKey'."
    }

    $valueOffset = ($cursor + 3) -band (-bnot [uint64]3)
    if ($valueOffset + $valueLength -gt $versionOffset + $versionLength) {
        throw 'Windows RT_VERSION fixed version metadata extends outside VS_VERSION_INFO.'
    }
    if ((Read-UInt32 -Offset $valueOffset -Description 'VS_FIXEDFILEINFO signature') -ne [uint32]4277077181) {
        throw 'Windows RT_VERSION resource does not contain a valid VS_FIXEDFILEINFO signature.'
    }

    function Format-FourPartVersion {
        param([uint32]$MostSignificant, [uint32]$LeastSignificant)

        return "$(($MostSignificant -shr 16) -band 0xFFFF).$($MostSignificant -band 0xFFFF).$(($LeastSignificant -shr 16) -band 0xFFFF).$($LeastSignificant -band 0xFFFF)"
    }

    $fileVersion = Format-FourPartVersion `
        -MostSignificant (Read-UInt32 -Offset ($valueOffset + 8) -Description 'VS_FIXEDFILEINFO file-version high value') `
        -LeastSignificant (Read-UInt32 -Offset ($valueOffset + 12) -Description 'VS_FIXEDFILEINFO file-version low value')
    $productVersion = Format-FourPartVersion `
        -MostSignificant (Read-UInt32 -Offset ($valueOffset + 16) -Description 'VS_FIXEDFILEINFO product-version high value') `
        -LeastSignificant (Read-UInt32 -Offset ($valueOffset + 20) -Description 'VS_FIXEDFILEINFO product-version low value')

    return [pscustomobject]@{
        FileVersion = $fileVersion
        ProductVersion = $productVersion
    }
}
