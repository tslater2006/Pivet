if (Test-Path ".\Pivet\bin\Release\net6.0\publish\") { 
    Remove-Item ".\Pivet\bin\Release\net6.0\" -Recurse -Force; 
}

If(!(test-path ".\build"))
{
      mkdir build
}

$commit= &git rev-parse HEAD 
$hash = $commit.Substring(0,7)

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=Linux /p:Configuration=Release -f net6.0
Compress-Archive -Force -Path .\Pivet\bin\Release\net6.0\linux-x64\* -DestinationPath ".\build\linux-$($hash).zip"

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=Windows /p:Configuration=Release -f net6.0
Compress-Archive -Force -Path .\Pivet\bin\Release\net6.0\win10-x64\* -DestinationPath ".\build\windows-$($hash).zip"

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=RedHat /p:Configuration=Release -f net6.0
Compress-Archive -Force -Path .\Pivet\bin\Release\net6.0\rhel-x64\* -DestinationPath ".\build\redhat-$($hash).zip"

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=LinuxSelfContained /p:Configuration=Release -f net6.0
Compress-Archive -Force -Path .\Pivet\bin\Release\net6.0\linux-x64\* -DestinationPath ".\build\linux-self-contained-$($hash).zip"

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=WindowsSelfContained /p:Configuration=Release -f net6.0
Compress-Archive -Force -Path .\Pivet\bin\Release\net6.0\win10-x64\* -DestinationPath ".\build\windows-self-contained-$($hash).zip"

dotnet publish .\Pivet\Pivet.csproj /p:PublishProfile=RedHatSelfContained /p:Configuration=Release -f net6.0
Compress-Archive -Force -Path .\Pivet\bin\Release\net6.0\rhel-x64\* -DestinationPath ".\build\redhat-self-contained-$($hash).zip"