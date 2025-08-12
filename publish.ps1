if (Test-Path ".\Pivet\bin\Release\net8.0\publish\") { 
    Remove-Item ".\Pivet\bin\Release\net8.0\" -Recurse -Force; 
}

If(!(test-path ".\build"))
{
      mkdir build
}

$commit= &git rev-parse HEAD 
$hash = $commit.Substring(0,7)

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=Linux /p:Configuration=Release -f net8.0
Compress-Archive -Force -Path .\Pivet\bin\Release\net8.0\linux-x64\* -DestinationPath ".\build\linux-$($hash).zip"

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=Windows /p:Configuration=Release -f net8.0
Compress-Archive -Force -Path .\Pivet\bin\Release\net8.0\win-x64\* -DestinationPath ".\build\windows-$($hash).zip"

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=LinuxSelfContained /p:Configuration=Release -f net8.0
Compress-Archive -Force -Path .\Pivet\bin\Release\net8.0\linux-x64\* -DestinationPath ".\build\linux-self-contained-$($hash).zip"

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=WindowsSelfContained /p:Configuration=Release -f net8.0
Compress-Archive -Force -Path .\Pivet\bin\Release\net8.0\win-x64\* -DestinationPath ".\build\windows-self-contained-$($hash).zip"