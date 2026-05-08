#scp -r /Users/nikita/RiderProjects/FanShop/* Nikita@192.168.0.13:C:/Users/Nikita/Documents/FanShopDebug/

ssh Nikita@192.168.0.13 "cd C:/Users/Nikita/Documents/FanShopDebug && dotnet build && dotnet run"