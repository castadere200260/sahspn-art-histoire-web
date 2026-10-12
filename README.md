# SAHSPN — Art & Histoire

Site responsive ASP.NET Core MVC (.NET 9) with a single, explicit persistence provider selected at startup. The MVC controllers depend only on `ISiteContentStore`; provider-specific code stays in `Storage/`.

## 1. Select the database

Edit **only** `Database:Provider` in `appsettings.json`:

```json
"Database": {
  "Provider": "MySql"
}
```

Use `MySql` for MySQL or change the value to `MongoDb` for MongoDB. The application registers and initializes only the selected provider. Unsupported values and missing selected-provider settings fail with a clear configuration error.

### MySQL settings

```json
"MySql": {
  "ConnectionString": "Server=127.0.0.1;Port=3306;Database=sahspn;User ID=sahsp_app;Password=CHANGE_ME_LOCAL_ONLY;CharSet=utf8mb4;"
}
```

### MongoDB settings

```json
"MongoDb": {
  "ConnectionString": "mongodb://127.0.0.1:27017",
  "DatabaseName": "sahspn"
}
```

Both provider configurations may remain in the file. Only the section selected by `Database:Provider` is used. For production, provide credentials through environment variables or a secret store, not source control.

## 2. Start the selected database

Docker Desktop must be running before executing Docker Compose commands.

### MySQL

```powershell
$env:Database__Provider = "MySql"
docker compose --profile mysql up -d mysql
docker compose ps
dotnet restore
dotnet build
dotnet run
```

The app reads `MySql:ConnectionString`. Set `SAHSP_MYSQL_PASSWORD` to the same password before starting the container and ensure the connection string uses that password. The Compose defaults are for local development only. If the existing volume was initialized with a different password, changing an environment variable does not change the existing MySQL user's password.

### MongoDB

```powershell
$env:Database__Provider = "MongoDb"
docker compose --profile mongodb up -d mongodb
docker compose ps
docker compose logs --tail 100 mongodb
dotnet restore
dotnet build
dotnet run
```

The app reads `MongoDb:ConnectionString` and `MongoDb:DatabaseName`. MongoDB creates collections and unique indexes, then seeds missing initial pages/bulletins from `seed-content.json`.

You can also set the provider in `appsettings.json` instead of an environment variable. ASP.NET Core environment variables override JSON configuration; clear any old `$env:Database__Provider` value if it causes confusion:

```powershell
Remove-Item Env:Database__Provider -ErrorAction SilentlyContinue
```

## 3. Architecture

- `Configuration/DatabaseOptions.cs`: provider selection.
- `Configuration/MySqlOptions.cs`: MySQL connection settings.
- `Configuration/MongoDbOptions.cs`: MongoDB connection settings.
- `Storage/ISiteContentStore.cs`: provider-neutral persistence contract.
- `Storage/MySqlSiteContentStore.cs`: EF Core/Pomelo implementation.
- `Storage/MongoSiteContentStore.cs`: MongoDB.Driver implementation.
- `Data/SahspDbContext.cs`, `Data/ContentSeeder.cs`: MySQL schema and seed data.
- `seed-content.json`: shared initial content.

The database provider is selected once at startup. Switching providers does not migrate data from one database to the other; each store must be seeded/imported separately. This project uses `EnsureCreated` for local bootstrap; production deployments should use reviewed, versioned EF Core migrations for MySQL and a deliberate index/schema deployment strategy for MongoDB.

## 4. Validation

```powershell
dotnet restore
dotnet build
dotnet run
```

Check `Database:Provider` and the selected server's availability if startup fails. Docker daemon errors must be resolved by starting Docker Desktop; they are independent of .NET compilation.


## Administration et authentification

Le lien **Connexion** est visible sur le site public. Le lien **Administration** apparaît uniquement après authentification, et les routes d'administration sont protégées côté serveur par `[Authorize(Roles = "Administrator")]`. La session utilise un cookie HttpOnly; les mots de passe sont stockés sous forme de hash ASP.NET Core Identity, jamais en clair.

### Provisionner le premier administrateur

Configurez le mot de passe initial via une variable d'environnement (ne le commitez pas dans `appsettings.json`) avant le premier démarrage :

PowerShell :

```powershell
$env:AdminBootstrap__Username = "admin"
$env:AdminBootstrap__Password = "<un-mot-de-passe-fort-et-unique>"
dotnet run
```

L'application crée ce compte dans la base sélectionnée uniquement si aucun compte portant cet identifiant n'existe. Le mot de passe est haché avec `PasswordHasher<TUser>`. Après le premier démarrage, retirez la variable `AdminBootstrap__Password`; le compte reste en base. Si aucun mot de passe initial n'est configuré, aucun compte n'est créé automatiquement.

Pour une utilisation en production, servez le site exclusivement en HTTPS, fournissez les secrets par un coffre de secrets ou des variables d'environnement, et mettez en place une procédure de rotation du mot de passe et de sauvegarde de la base.


> Note technique : la création de la table `AdminUsers` est compatible avec une base MySQL déjà initialisée par la version précédente. Pour les évolutions de schéma suivantes, utiliser des migrations EF Core versionnées.

## Gestion des fichiers des bulletins

Dans l'administration, le formulaire de bulletin accepte maintenant le téléversement d'une couverture (JPG, JPEG, PNG ou WebP, 8 Mo maximum) et d'un document PDF (25 Mo maximum). Le formulaire utilise `multipart/form-data`; les fichiers sont stockés sous `wwwroot/uploads/covers` et `wwwroot/uploads/bulletins`, et les chemins relatifs sont enregistrés dans la base sélectionnée.

Lors d'une modification, si aucun nouveau fichier n'est choisi, le fichier existant est conservé. En déploiement Docker ou multi-instance, configurez un volume persistant pour `wwwroot/uploads` ou remplacez ce stockage local par un stockage objet partagé.

La validation côté serveur contrôle l'extension et la taille. Pour un déploiement exposé à Internet, il est recommandé d'ajouter une vérification du contenu réel des fichiers (signature MIME), un antivirus et une politique de rétention des fichiers remplacés.
