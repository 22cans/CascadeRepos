#testCommand=$( dotnet test --collect:"XPlat Code Coverage" )
testCommand=$( dotnet test -s CodeCoverage.runsettings )

report=""
getnext=false
while IFS= read -r line; do
    if $getnext ; then
        reportName=$( echo $line | awk '{$1=$1;print}' )
        report="${report}${reportName};"
        getnext=false
    fi
    if [[ "$line" =~ ^Attachments:.* ]]; then
        getnext=true
        continue
    fi
done <<< "$testCommand"

reportgenerator -reports:"$report" -targetdir:"coveragereport" -reporttypes:Html

rm -rf tests/Legacy.Application.Tests/TestResults
rm -rf tests/Cascade.UnitTests/TestResults

start ./coveragereport/index.html
open ./coveragereport/index.html
