if (Test-Path ".\Pivet\bin\Release\netcoreapp3.0\publish\") { 
    Remove-Item ".\Pivet\bin\Release\netcoreapp3.0\publish\" -Recurse -Force; 
}

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=Linux /p:Configuration=Release -f netcoreapp3.0
dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=Windows /p:Configuration=Release -f netcoreapp3.0
dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=RedHat /p:Configuration=Release -f netcoreapp3.0

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=LinuxSelfContained /p:Configuration=Release -f netcoreapp3.0
dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=WindowsSelfContained /p:Configuration=Release -f netcoreapp3.0
dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=RedHatSelfContained /p:Configuration=Release -f netcoreapp3.0

If(!(test-path ".\build"))
{
      mkdir build
}

$commit= &git rev-parse HEAD 
$hash = $commit.Substring(0,7)

Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp3.0\publish\linux\* -DestinationPath ".\build\linux-$($hash).zip"
Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp3.0\publish\windows\* -DestinationPath ".\build\windows-$($hash).zip"
Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp3.0\publish\rhel\* -DestinationPath ".\build\redhat-$($hash).zip"

Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp3.0\publish\linux-self-contained\* -DestinationPath ".\build\linux-self-contained-$($hash).zip"
Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp3.0\publish\windows-self-contained\* -DestinationPath ".\build\windows-self-contained-$($hash).zip"
Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp3.0\publish\rhel-self-contained\* -DestinationPath ".\build\redhat-self-contained-$($hash).zip"