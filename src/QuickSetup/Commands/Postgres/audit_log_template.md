# AUDIT LOG

**Audit log `{currentDatabase}.{currentSchema}`**

## Used command

{commandLine}

## Used settings
```
{settingsJson}
```

## Users

{usersTable}

## Connection strings

```
{connectionStrings}
```

## Execution log

### Exceptions

{exception}

### Create users

Connect to the default database (e.g. 'postgres') as administrator and issue the following commands:

```
{userCreationStatements}
```

### Create database

Connect to the default database (e.g. 'postgres') as administrator and issue the following commands:

```
{databaseCreationStatements}
```

### Create schema

Connect to database '{currentDatabase}' as administrator and issue the following commands:

```
{schemaCreationStatements}
```

### Create extensions

Connect to database '{currentDatabase}.{currentSchema}' as administrator and issue the following commands:

```
{extensionCreationStatements}
```

### Grant usages

Connect to database '{currentDatabase}' as administrator and issue the following commands:

```
{grantUsageStatements}
```

### Grant default privileges

Connect to database '{currentDatabase}' as {dbObjectsOwner} and issue the following commands:

```
{grantDefaultPrivilegesStatements}
```