# TopMail.Rest

![.NET](https://img.shields.io/badge/.NET-8-blue)
![CSharp](https://img.shields.io/badge/C%23-12-239120)
![API](https://img.shields.io/badge/API-REST-0aa2c0)
![Security](https://img.shields.io/badge/security-HMAC%20%2B%20RateLimit-success)
![Mail](https://img.shields.io/badge/mail-SMTP%20OAuth2-6f42c1)
![Auth](https://img.shields.io/badge/auth-ApiKey%20HMAC-orange)
![Logging](https://img.shields.io/badge/logging-File%20%2B%20Structured-4c1)
![Platform](https://img.shields.io/badge/platform-ASP.NET%20Core-5C2D91)

API REST .NET 8 per invio email via SMTP OAuth2 (Microsoft 365), con autenticazione HMAC, policy per client, rate limiting e logging su file.

## Panoramica

Funzionalita principali:

- invio email JSON e multipart/form-data
- autenticazione custom ApiKey + HMAC SHA256
- protezione anti-replay (timestamp + nonce)
- rate limiting per client autenticato
- validazione policy mittenti/destinatari per client
- supporto allegati e immagini inline (multipart)
- fallback mittente in caso di SendAsDenied
- logging applicativo strutturato su file configurabile

## Endpoint

Base route: `api/mail`

- `POST /api/mail/send`
- `POST /api/mail/send-multipart`

Nota: gli endpoint richiedono autenticazione HMAC (`401` se header mancanti/non validi).

## Sicurezza

Schema di autenticazione: `ApiKeyHmac`

Header richiesti:

- `X-Client-Id`
- `X-Api-Key`
- `X-Timestamp` (UTC ISO-8601)
- `X-Nonce`
- `X-Signature` (Base64 HMAC-SHA256)

Canonical string usata per la firma:

```text
{METHOD_UPPER}
{PATH_AND_QUERY}
{BASE64_SHA256_BODY}
{TIMESTAMP_HEADER}
{NONCE_HEADER}
{CLIENT_ID}
```

Controlli aggiuntivi:

- finestra temporale (`AllowedClockSkewSeconds`)
- anti-replay nonce (`NonceTtlSeconds`)
- rate limit globale client autenticati: `MaxRequestsPerMinute` per client
- rate limit richieste anonime/non autenticate: limite ridotto per IP

## Payload

### JSON (`POST /api/mail/send`)

```json
{
  "mittente": "noreply@example.com",
  "destinatario": ["user@example.com"],
  "cc": [],
  "ccn": [],
  "replyTo": "",
  "oggetto": "Oggetto",
  "testoMail": "PGh0bWw+PGJvZHk+Q2lhbyE8L2JvZHk+PC9odG1sPg==",
  "typeBody": "1"
}
```

### Multipart (`POST /api/mail/send-multipart`)

Campi form supportati:

- `mittente`
- `destinatario` (lista separata da `,` o `;`)
- `cc`
- `ccn`
- `replyTo`
- `oggetto`
- `testoMail`
- `typeBody`
- `files` (allegati)
- `inlineFiles` (immagini inline)
- `inlineCids` (CID separati da `,` o `;`)

## Gestione body HTML (`typeBody`)

Quando `typeBody` e `1` (o `true`/`html`):

1. `testoMail` deve essere Base64 UTF-8
2. il backend decodifica Base64
3. applica `HtmlDecode` (entita come `&lt;`, `&gt;`, `&amp;`)
4. opzionalmente sanitizza/wrappa HTML in base a configurazione `MailBody`

Se `typeBody` non e HTML, il body e trattato come testo semplice.

## Codici risposta

- `200` invio riuscito
- `400` errore validazione input/body/allegati
- `401` autenticazione HMAC fallita
- `403` policy client non autorizza la richiesta
- `429` rate limit superato
- `500` errore interno/configurazione/SMTP

## Configurazione (`appsettings*.json`)

Sezioni principali:

- `Smtp`
- `AzureAd`
- `Tracking`
- `MailBody`
- `AttachmentSecurity`
- `AuthorizedClients`
- `Logging`
- `FileLogging`

Esempio sintetico:

```json
{
  "Smtp": {
    "Host": "smtp.office365.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "mailbox@domain.tld"
  },
  "AzureAd": {
    "TenantId": "...",
    "ClientId": "...",
    "ClientSecret": "..."
  },
  "AuthorizedClients": {
    "AllowedClockSkewSeconds": 300,
    "NonceTtlSeconds": 300,
    "Clients": [
      {
        "ClientId": "client-1",
        "ApiKey": "...",
        "HmacSecret": "...",
        "Enabled": true,
        "MaxRequestsPerMinute": 60,
        "AllowedFromAddresses": [],
        "AllowedRecipientDomains": []
      }
    ]
  },
  "FileLogging": {
    "Enabled": true,
    "DirectoryPath": "c:/temp/topmail-logs",
    "FileNamePrefix": "topmail",
    "FileNameDateFormat": "yyyyMMdd",
    "MinimumLevel": "Information"
  }
}
```

## Logging

Il progetto usa `ILogger` + provider custom su file.

- output su file giornaliero nella directory `FileLogging:DirectoryPath`
- log di pipeline su controller + servizio SMTP
- separatore esplicito a inizio richiesta (`BEGIN MAIL REQUEST`)
- log body HTML raw/decoded disponibili solo a livello `Debug`

Per abilitare debug completo (anche body):

```json
{
  "Logging": {
    "LogLevel": {
      "TopMail.Rest": "Debug"
    }
  },
  "FileLogging": {
    "MinimumLevel": "Debug"
  }
}
```

## Avvio locale

Richiede .NET SDK 8.0+

```bash
dotnet restore
dotnet build
dotnet run
```

## Dipendenze principali

- `MailKit`
- `Microsoft.Identity.Client`
- `HtmlSanitizer`

## Note operative

- il mittente effettivo puo andare in fallback su `Smtp:Username` in caso di errore `SendAsDenied`
- `Tracking:BccAddress` viene aggiunto automaticamente se configurato e valido
- gli indirizzi destinatari sono normalizzati e deduplicati tra To/Cc/Bcc
- limiti allegati applicati da `AttachmentSecurity` (count, size, estensioni)
