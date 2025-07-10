# AUDIT LOG

**Audit log `{currentDatabase}.{currentSchema}`**

## Used settings

{settingsTable}

## Users

{usersTable}

## Connection strings

```
{connectionStrings}
```

## Execution log

### Exceptions

{exception}

### Drop database objects

{ if dropDatabaseStatementSkipped:
All optional deletion flags are to 'false'. Skipping removal of existing objects.
|else:

* Connect to database '{currentDatabase}' as administrator and issue the following commands:

```
{dropSchemaStatement}
```

* Connect to a default database (e.g. 'postgres') as administrator and issue the following commands:

```
{dropDatabaseStatement}
```

* Connect to the default database (e.g. 'postgres') as administrator and issue the following commands:

```
{dropUserStatement}
```

}

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