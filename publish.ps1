if (Test-Path ".\Pivet\bin\Release\netcoreapp2.2\publish\") { 
    Remove-Item ".\Pivet\bin\Release\netcoreapp2.2\publish\" -Recurse -Force; 
}

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=Linux /p:Configuration=Release -f netcoreapp2.2
dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=Windows /p:Configuration=Release -f netcoreapp2.2
dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=RedHat /p:Configuration=Release -f netcoreapp2.2

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=LinuxSelfContained /p:Configuration=Release -f netcoreapp2.2
dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=WindowsSelfContained /p:Configuration=Release -f netcoreapp2.2
dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=RedHatSelfContained /p:Configuration=Release -f netcoreapp2.2

If(!(test-path ".\build"))
{
      mkdir build
}

$commit= &git rev-parse HEAD 
$hash = $commit.Substring(0,7)

Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp2.2\publish\linux\* -DestinationPath ".\build\linux-$($hash).zip"
Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp2.2\publish\windows\* -DestinationPath ".\build\windows-$($hash).zip"
Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp2.2\publish\rhel\* -DestinationPath ".\build\redhat-$($hash).zip"

Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp2.2\publish\linux-self-contained\* -DestinationPath ".\build\linux-self-contained-$($hash).zip"
Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp2.2\publish\windows-self-contained\* -DestinationPath ".\build\windows-self-contained-$($hash).zip"
Compress-Archive -Force -Path .\Pivet\bin\Release\netcoreapp2.2\publish\rhel-self-contained\* -DestinationPath ".\build\redhat-self-contained-$($hash).zip"