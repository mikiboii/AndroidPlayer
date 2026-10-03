dotnet publish -r win-x64 -c Release ^
  -p:PublishAot=true ^
  -p:IsAotCompatible=true ^
  -p:BuiltInComInteropSupport=false ^
  -p:SelfContained=true ^
  -p:DebugType=none ^
  -p:DebugSymbols=false -o publish-test/
  
  
  
  
  
@REM   dotnet publish -r win-x64 -c Release ^
@REM     -p:SelfContained=true ^
@REM     -p:DebugType=none ^
@REM     -p:DebugSymbols=false -o publish/
    