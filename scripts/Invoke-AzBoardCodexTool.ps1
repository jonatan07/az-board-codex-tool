[CmdletBinding()]
param(
    [ValidateSet("create", "update", "get", "link-parent", "query", "help")]
    [string]$Command = "help",

    [string]$Type,
    [string]$Title,
    [string]$Description,
    [string]$AssignedTo,
    [string]$AcceptanceCriteria,
    [string]$Tags,
    [int]$Id,
    [string]$State,
    [int]$ChildId,
    [int]$ParentId,
    [string]$Wiql,

    [ValidateSet("DotNet", "Docker")]
    [string]$Runtime = "DotNet",

    [string]$ImageName = "az-board-codex-tool:latest",
    [switch]$CheckEnvironment
)

$ErrorActionPreference = "Stop"
$requiredVariables = @("AZDO_ORG", "AZDO_PROJECT", "AZDO_PAT")

function Import-PersistedEnvironmentVariable {
    param([Parameter(Mandatory)][string]$Name)

    $currentValue = [Environment]::GetEnvironmentVariable($Name, "Process")
    if (-not [string]::IsNullOrWhiteSpace($currentValue)) {
        return
    }

    foreach ($target in @("User", "Machine")) {
        $persistedValue = [Environment]::GetEnvironmentVariable($Name, $target)
        if (-not [string]::IsNullOrWhiteSpace($persistedValue)) {
            [Environment]::SetEnvironmentVariable($Name, $persistedValue, "Process")
            return
        }
    }
}

foreach ($variableName in $requiredVariables) {
    Import-PersistedEnvironmentVariable -Name $variableName
}

$missingVariables = @(
    $requiredVariables | Where-Object {
        [string]::IsNullOrWhiteSpace(
            [Environment]::GetEnvironmentVariable($_, "Process"))
    }
)

if ($missingVariables.Count -gt 0) {
    [Console]::Error.WriteLine(
        "Missing required environment variables: {0}" -f ($missingVariables -join ", "))
    exit 2
}

if ($CheckEnvironment) {
    Write-Output "Azure DevOps environment configuration is available."
    exit 0
}

function Add-StringArgument {
    param(
        [Parameter(Mandatory)][System.Collections.Generic.List[string]]$Arguments,
        [Parameter(Mandatory)][string]$Name,
        [AllowNull()][string]$Value
    )

    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        $Arguments.Add($Name)
        $Arguments.Add($Value)
    }
}

$toolArguments = [System.Collections.Generic.List[string]]::new()

switch ($Command) {
    "help" {
        $toolArguments.Add("--help")
    }
    "create" {
        if ([string]::IsNullOrWhiteSpace($Type) -or [string]::IsNullOrWhiteSpace($Title)) {
            throw "Command 'create' requires -Type and -Title."
        }

        $toolArguments.Add("create")
        Add-StringArgument $toolArguments "--type" $Type
        Add-StringArgument $toolArguments "--title" $Title
        Add-StringArgument $toolArguments "--description" $Description
        Add-StringArgument $toolArguments "--assigned-to" $AssignedTo
        Add-StringArgument $toolArguments "--acceptance-criteria" $AcceptanceCriteria
        Add-StringArgument $toolArguments "--tags" $Tags
    }
    "update" {
        if ($Id -le 0) {
            throw "Command 'update' requires a positive -Id."
        }

        $toolArguments.Add("update")
        $toolArguments.Add("--id")
        $toolArguments.Add($Id.ToString())
        Add-StringArgument $toolArguments "--title" $Title
        Add-StringArgument $toolArguments "--description" $Description
        Add-StringArgument $toolArguments "--state" $State
        Add-StringArgument $toolArguments "--tags" $Tags
    }
    "get" {
        if ($Id -le 0) {
            throw "Command 'get' requires a positive -Id."
        }

        $toolArguments.Add("get")
        $toolArguments.Add("--id")
        $toolArguments.Add($Id.ToString())
    }
    "link-parent" {
        if ($ChildId -le 0 -or $ParentId -le 0) {
            throw "Command 'link-parent' requires positive -ChildId and -ParentId values."
        }

        $toolArguments.Add("link-parent")
        $toolArguments.Add("--child-id")
        $toolArguments.Add($ChildId.ToString())
        $toolArguments.Add("--parent-id")
        $toolArguments.Add($ParentId.ToString())
    }
    "query" {
        if ([string]::IsNullOrWhiteSpace($Wiql)) {
            throw "Command 'query' requires -Wiql."
        }

        $toolArguments.Add("query")
        Add-StringArgument $toolArguments "--wiql" $Wiql
    }
}

if ($Runtime -eq "Docker") {
    & docker run --rm `
        --env AZDO_ORG `
        --env AZDO_PROJECT `
        --env AZDO_PAT `
        $ImageName @toolArguments
}
else {
    $projectPath = Join-Path (Split-Path -Parent $PSScriptRoot) "AzBoardCodexTool.csproj"
    & dotnet run `
        --project $projectPath `
        --configuration Release `
        -- @toolArguments
}

exit $LASTEXITCODE
