# ==========================================
# Build stage
# ==========================================

# SDK image 包含：
#
# dotnet restore
# dotnet build
# dotnet publish
#
# 這一層只負責編譯，
# 最後不會成為正式執行的 container image。
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build


# Container build stage 裡的工作目錄。
WORKDIR /src


# ==========================================
# Restore NuGet packages
# ==========================================

# 先只複製 .csproj。
#
# 好處是：
#
# 如果程式碼改了，
# 但是 csproj / NuGet dependencies 沒變，
# Docker 可以重用 restore layer cache。
COPY RazorCommerceLab/RazorCommerceLab.csproj RazorCommerceLab/


RUN dotnet restore \
  RazorCommerceLab/RazorCommerceLab.csproj


# ==========================================
# Copy source code
# ==========================================

# 把 build context 中其餘檔案複製進 image。
#
# 哪些檔案不要複製，
# 等一下由 .dockerignore 控制。
COPY . .


# 切到 ASP.NET Core project。
WORKDIR /src/RazorCommerceLab


# ==========================================
# Publish
# ==========================================

# Release：
# 正式發布組態。
#
# -o /app/publish：
# publish 結果統一放在這裡。
#
# UseAppHost=false：
# 不另外產生平台專用 executable，
# 最後直接：
#
# dotnet RazorCommerceLab.dll
#
# 啟動。
RUN dotnet publish RazorCommerceLab.csproj \
  -c Release \
  -o /app/publish \
  /p:UseAppHost=false


# ==========================================
# Runtime stage
# ==========================================

# 正式執行網站不需要完整 SDK。
#
# aspnet image 只包含 ASP.NET Core
# 執行階段所需要的 runtime / libraries。
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime


WORKDIR /app


# 從 build stage 只複製 publish 結果。
COPY --from=build /app/publish .


# SQLite database 會放：
#
# /app/Data/razor-commerce.db
RUN mkdir -p /app/Data


# ASP.NET Core container 內使用 8080。
EXPOSE 8080


# ==========================================
# Application startup
# ==========================================

# Container 啟動時執行：
#
# dotnet RazorCommerceLab.dll
ENTRYPOINT ["dotnet", "RazorCommerceLab.dll"]
