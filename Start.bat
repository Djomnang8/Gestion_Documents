@echo off
title Lanceur - Gestion Documents
echo ===================================================
echo  Lancement du backend et du frontend...
echo ===================================================
echo.

REM --- Aller dans le dossier du frontend ---
cd /d "C:\TP_PROJET_B1_a_B3\projet_Angular\gestion_documents_C_sharp\frontend\gestion-documents-front"

REM --- Supprimer les caches (solution aux erreurs de pré-bundle) ---
echo Suppression du cache Angular et Vite...
rmdir /s /q .angular 2>nul
rmdir /s /q node_modules\.vite 2>nul
echo Caches supprimes.
echo.

REM --- Lancer le backend ---
echo Demarrage du backend...
start "Backend API" cmd /k "cd /d "C:\TP_PROJET_B1_a_B3\projet_Angular\gestion_documents_C_sharp\backend\GestionDocuments.API" && dotnet run"

REM --- Lancer le frontend ---
echo Demarrage du frontend...
start "Frontend Angular" cmd /k "cd /d "C:\TP_PROJET_B1_a_B3\projet_Angular\gestion_documents_C_sharp\frontend\gestion-documents-front" && ng serve --open"

echo.
echo ===================================================
echo  Les services demarrent...
echo  Le navigateur va s'ouvrir automatiquement.
echo ===================================================
timeout /t 10 /nobreak >nul

REM --- Ouvrir le navigateur (secours) ---
start http://localhost:4200

echo.
echo Application lancee. Vous pouvez fermer cette fenetre.
pause