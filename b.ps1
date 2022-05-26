$ErrorActionPreference = "Stop"

dotnet tool install --global dotnet-sonarscanner --version 5.5.3
dotnet sonarscanner begin /o:"cortside" /k:"cortside_cortside.domainevent" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.login="88c88c857e5710d2a8ec472b7b5c2e91eb79fc4d" /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml"
dotnet build src --configuration Debug /property:Version=1.1.174
dotnet test src --no-restore --no-build --collect:"XPlat Code Coverage" --settings ./src/coverlet.runsettings.xml /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
dotnet sonarscanner end /d:sonar.login="88c88c857e5710d2a8ec472b7b5c2e91eb79fc4d"

gci -Path . -File -Recurse *.xml | Select Name
