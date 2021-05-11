# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.

# This script is used to copy unique files to a directory and saving the mapping for use later. It is intended to help reduce the burden on the ESRP signing service.
[CmdletBinding(DefaultParameterSetName = 'Find')]
Param
(
    [Parameter(ParameterSetName = 'Find')][string]$MappingFileOut,
    [Parameter(ParameterSetName = 'Find')][string]$SearchFilter = "*",
    [Parameter(ParameterSetName = 'Find')][string]$CopyDestination,
    [Parameter(ParameterSetName = 'CopyMapping')][string]$MappingFileIn,
    [Parameter(ParameterSetName = 'Find')][Parameter(ParameterSetName = 'CopyMapping')][string]$CopySource
)

# Ensure paths use consistent separator char
$MappingFileOut = $MappingFileOut.Replace('\', '/')
$MappingFileIn = $MappingFileIn.Replace('\', '/')
$CopyDestination = $CopyDestination.Replace('\', '/')
$CopySource = $CopySource.Replace('\', '/')

Write-Host "params:"
Write-Host "MappingFileOut: $MappingFileOut"
Write-Host "SearchFilter: $SearchFilter"
Write-Host "CopyDestination: $CopyDestination"
Write-Host "MappingFileIn: $MappingFileIn"
Write-Host "CopySource: $CopySource"
$ErrorActionPreference = "stop"

# create mapping and copy unique files
if ($PSCmdlet.ParameterSetName -eq 'Find')
{
    if ((Test-Path $CopyDestination) -eq $false)
    {
        $null = New-Item -Path $CopyDestination -ItemType Directory
    }

    $files = Get-ChildItem $CopySource -Filter $SearchFilter -Recurse -File
    # hashtable of file names & array of full paths that file is found in
    $mapping = @{}
    foreach ($file in $files)
    {
        # Find hash of file. If it's new, add new kvp to mapping & copy file with uniqified name. Else add to value array.
        $hash = (Get-FileHash $file).Hash
    
        if ($mapping.ContainsKey("$($hash)__$($file.Name)") -eq $false)
        {
            Write-Host "Copying unique file $($file.FullName) wuth hash $hash to $CopyDestination\$($hash)__$($file.Name)"
            $mapping.Add("$($hash)__$($file.Name)", @($file.FullName))
            Copy-Item $file "$CopyDestination\$($hash)__$($file.Name)"
        }
        else
        {
            $currentArray = $mapping["$($hash)__$($file.Name)"]
            $currentArray += $file.FullName
            $mapping["$($hash)__$($file.Name)"] = $currentArray
            Write-Host "Adding mapping for file $($file.FullName) with hash $hash"
        }
    }

    Get-ChildItem $CopyDestination
    
    # serialize mapping to file so we can use after signing
    $OutFolder = [System.IO.Path]::GetDirectoryName($MappingFileOut)
    if ((Test-Path $OutFolder) -eq $false)
    {
        $null = New-Item -Path $OutFolder -ItemType Directory
    }
    Export-Clixml -InputObject $mapping -Depth 5 -Path $MappingFileOut
}
else # use mapping to copy unique files to all locations. Done after files are signed.
{
    # deserialize mapping
    $mapping = Import-Clixml -Path $MappingFileIn
    
    # get all files to copy
    $files = Get-ChildItem $CopySource -Recurse -File
    Write-Host "$($files.Count) files found in $CopySource"

    foreach ($file in $files)
    {
        if ($mapping.ContainsKey($file.Name) -eq $true)
        {
            # if file is in mapping, copy to destinations specified
            foreach ($dest in $mapping[$file.Name])
            {
                Write-Host "Copying file $($file.FullName) to $dest"
                Copy-Item $file $dest
            }
        }
    }
}
