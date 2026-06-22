# Gestion Documents Symphony — Guide de démarrage

## Prérequis

- **PHP 8.2+** avec extensions : pdo_mysql, openssl, mbstring, intl, zip
- **Composer** (https://getcomposer.org)
- **Node.js 20+** et npm
- **XAMPP** (MariaDB/MySQL sur port 3306)
- **Symfony CLI** : https://symfony.com/download
- **Android Studio** (pour la version mobile)

---

## 1. Base de données

Démarrer XAMPP → Apache + MySQL, puis :

```cmd
mysql -u root -p < backend-symfony\database\Gestion_Documents_MySQL.sql
```

La base `gestion_documents` est créée avec toutes les tables, rôles et comptes.

**Comptes disponibles :**

| Rôle           | Email                    | Mot de passe   |
|----------------|--------------------------|----------------|
| Administrateur | mbogo@gmail.com          | Admin1234@     |
| Agent          | jean.dupont@ged.local    | Agent1234@     |
| Agent          | atangana@gmail.com       | Agent1234@     |
| Archiviste     | martin@gmail.com         | Archiviste1@   |

---

## 2. Backend Symfony

```cmd
cd backend-symfony

composer install

:: Vérifier le fichier .env (DATABASE_URL et JWT_SECRET_KEY)
:: DATABASE_URL="mysql://root:@127.0.0.1:3306/gestion_documents"

:: Générer les clés JWT si absentes
php bin/console lexik:jwt:generate-keypair

:: Démarrer le serveur Symfony sur le port 8001
symfony serve --port=8001 --no-tls
```

> Backend accessible sur : **http://127.0.0.1:8001**

---

## 3. Frontend Angular (navigateur web)

```cmd
cd frontend\gestion-documents-front

npm install

npm start
```

> Application accessible sur : **http://localhost:4200**

---

## 4. Build APK Android

### 4a. Build Angular pour mobile

```cmd
cd frontend\gestion-documents-front

npx ng build --configuration=mobile
```

> Si la configuration `mobile` n'existe pas dans `angular.json`, l'ajouter sous
> `projects > gestion-documents-front > architect > build > configurations` :
> ```json
> "mobile": {
>   "fileReplacements": [{
>     "replace": "src/environments/environment.ts",
>     "with": "src/environments/environment.mobile.ts"
>   }],
>   "outputHashing": "all"
> }
> ```

### 4b. Synchroniser Capacitor

```cmd
npx cap sync android
```

### 4c. Ouvrir et générer l'APK dans Android Studio

```cmd
npx cap open android
```

Dans Android Studio :
1. Attendre la fin du chargement Gradle
2. **Build → Build Bundle(s) / APK(s) → Build APK(s)**
3. L'APK se trouve dans :
   `android\app\build\outputs\apk\debug\app-debug.apk`

---

## 5. Installer l'APK sur un téléphone Android physique

### 5a. Activer le mode développeur sur le téléphone

1. **Paramètres → À propos du téléphone → Numéro de build** : taper 7 fois
2. **Paramètres → Options développeur** → activer **Débogage USB**
3. Connecter le téléphone au PC par câble USB
4. Accepter "Autoriser le débogage USB ?" sur le téléphone

### 5b. Installer via Android Studio

Dans Android Studio, sélectionner l'appareil dans la liste déroulante en haut,
puis cliquer **Run ▶** (Shift+F10).

### 5c. Installer l'APK via ADB (alternative)

```cmd
:: ADB est installé avec Android Studio, ex : C:\Users\<vous>\AppData\Local\Android\Sdk\platform-tools
adb install android\app\build\outputs\apk\debug\app-debug.apk
```

Ou copier l'APK sur le téléphone et l'ouvrir depuis le gestionnaire de fichiers
(autoriser "Sources inconnues" si demandé).

---

## 6. Connexion mobile ↔ backend PC Windows 11

Le téléphone et le PC doivent être sur le **même réseau WiFi**.

### 6a. Trouver l'IP WiFi du PC

```cmd
ipconfig
```

Repérer `Adresse IPv4` de l'adaptateur WiFi, ex : `192.168.1.187`.

### 6b. Autoriser le port 8001 dans le pare-feu Windows

```cmd
:: Exécuter en tant qu'administrateur
netsh advfirewall firewall add rule name="Symfony 8001" dir=in action=allow protocol=TCP localport=8001
```

### 6c. Démarrer Symfony en acceptant les connexions réseau

```cmd
cd backend-symfony
symfony serve --port=8001 --no-tls
```

> Symfony CLI écoute sur toutes les interfaces réseau automatiquement.
> Alternative : `php -S 0.0.0.0:8001 -t public`

### 6d. Configurer l'IP dans l'application mobile

1. Ouvrir l'appli sur le téléphone et se connecter
2. Dans la barre latérale → bouton **Paramètres réseau** (icône engrenage, en bas)
3. Sélectionner l'IP WiFi du PC (ex : `http://192.168.1.187:8001`)
   ou en ajouter une personnalisée
4. L'IP est sauvegardée automatiquement dans le stockage local

> En cas de changement de réseau WiFi, revenir dans "Paramètres réseau" et
> sélectionner/ajouter la nouvelle IP.

---

## 7. Flux de travail complet

```
Citoyen (page publique)
  └─ Dépose ses fichiers sur /depot (1 à 4 fichiers PDF/image)
  └─ Reçoit un numéro de dossier (ex: DOS-2026-00001)

Agent
  └─ Consulte ses dossiers dans /agent/dossiers
  └─ Marque le dossier TERMINÉ

Archiviste
  └─ Voit les dossiers TERMINÉS dans "Dossiers à archiver"
  └─ Archive le dossier → "Version 1" créée, dossier disparaît de l'espace Agent

  Si le même citoyen (même email + même nom) redépose des fichiers
  pour le même service et que ce nouveau dossier est archivé :
  └─ "Version 2" créée (devient la version active)
  └─ Et ainsi de suite (Version 3, 4...)

  Dans "Historique des versions" :
  └─ Arbre : Service → Nom du Citoyen → Version 1, Version 2, ...
  └─ Chaque version affiche ses fichiers (cliquer pour ouvrir/télécharger)
  └─ "Restaurer" rend une ancienne version active

  Dans "Archives Consultables" :
  └─ Toujours la version active par citoyen avec ses fichiers consultables
```

---

## 8. Commandes de référence

```cmd
:: Vider le cache Symfony
php bin/console cache:clear

:: Vérifier les routes API
php bin/console debug:router | findstr api

:: Lister les appareils Android connectés
adb devices

:: Voir les logs de l'appli Capacitor sur le téléphone
adb logcat -s Capacitor

:: Angular — recompiler en temps réel pour mobile
cd frontend\gestion-documents-front
npx ng build --watch --configuration=mobile
```

==
vider le localStorage
localStorage.removeItem('gestion_docs_api_url');
localStorage.removeItem('gestion_docs_api_custom_urls');
location.reload();
==
