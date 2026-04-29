# TopMail.Rest (.NET 8, REST)

Progetto REST moderno che replica le WebMethod ASMX esistenti in `TopMail.asmx`:

- GET `/api/hello` ? `HelloWorld()` ? ritorna "Hello World" (text/plain)
- POST `/api/mail/send` ? `InvioMail`
- POST `/api/mail/send-notracking` ? `InvioMailNoTracking`
- POST `/api/mail/send-ccn` ? `InvioMailConCCN` (aggiunge anche il tracking BCC da config)
- POST `/api/mail/send-ccn-notracking` ? `InvioMailConCCNNoTracking`
- POST `/api/mail/send-with-files` ? `InvioMailWithFiles`
- POST `/api/mail/send-with-files-ccn` ? `InvioMailWithFilesAndCCN`

Tutti gli endpoint restituiscono `"1"` su successo e `"-1"` su errore (content-type `text/plain`).

## Payload JSON

Esempi minimi:

```json
POST /api/mail/send
{
  "mittente": "noreply@example.com",
  "destinatario": "user@example.com",
  "oggetto": "Oggetto",
  "testoMail": "Testo",
  "typeBody": "1" // 1 = HTML, 0/empty = testo
}
```

```json
POST /api/mail/send-ccn
{
  "mittente": "noreply@example.com",
  "destinatario": "user@example.com",
  "ccn": "audit@example.com",
  "oggetto": "Oggetto",
  "testoMail": "<b>HTML</b>",
  "typeBody": "1"
}
```

```json
POST /api/mail/send-with-files
{
  "mittente": "noreply@example.com",
  "destinatario": "user@example.com",
  "oggetto": "Allegati",
  "testoMail": "Ciao",
  "typeBody": "0",
  "attachments": [
    { "name": "prova.txt", "base64": "SGVsbG8gV29ybGQh" }
  ]
}
```

> Nota: `InvioMailConCCN` aggiunge sempre anche il BCC di tracking configurato (come l'ASMX originario).

## Configurazione SMTP

Modifica `TopMail.Rest/appsettings.json`:

- `Smtp:Host` (es. `smtp.server.local`)
- `Smtp:Port` (es. `587`)
- `Smtp:EnableSsl` (`true/false`)
- `Smtp:Username` / `Smtp:Password` (se richieste credenziali)
- `Smtp:UseDefaultCredentials` (`true` se usi credenziali di macchina)
- `Smtp:DefaultSender` (opzionale; se il server non consente mittenti arbitrari)
- `Tracking:BccAddress` (default: `traccia-pl-lp@asl5.liguria.it`)
- `logFile`: `TUTTO` | `ERRORI` | `NIENTE`

Il logging replica il formato dell\'ASMX e scrive su `c:/temp/{0}_topmail.log` come nel codice originale.

## Avvio locale

Richiede .NET SDK 8.0:

```
cd TopMail.Rest
 dotnet run
```

Poi chiama gli endpoint:

```
curl http://localhost:5180/api/hello
```

## Note di compatibilità

- Questa implementazione non dipende dalla vecchia `Common.dll` (non compatibile con .NET 8). L\'invio usa `System.Net.Mail`.
- `maxRequestLength` dell\'ASMX (~50 MB) può essere riprodotto regolando la dimensione massima della richiesta a livello di reverse proxy/Kestrel se necessario.
- Se vuoi mantenere esattamente lo stesso nome file di log (con `{0}` letterale), è già così; possiamo cambiarlo per includere la data su richiesta.
### Multipart (form-data)

- POST `/api/mail/send-with-files-multipart`
- POST `/api/mail/send-with-files-ccn-multipart`

Esempio con `curl` (Windows PowerShell):

```
curl -X POST http://localhost:5180/api/mail/send-with-files-multipart ^
  -H "Content-Type: multipart/form-data" ^
  -F mittente=noreply@example.com ^
  -F destinatario=user@example.com ^
  -F oggetto=Allegati ^
  -F testoMail=Ciao ^
  -F typeBody=0 ^
  -F files=@C:\\path\\to\\prova.txt
```

Con CCN:

```
curl -X POST http://localhost:5180/api/mail/send-with-files-ccn-multipart ^
  -H "Content-Type: multipart/form-data" ^
  -F mittente=noreply@example.com ^
  -F destinatario=user@example.com ^
  -F ccn=audit@example.com ^
  -F oggetto=Allegati ^
  -F testoMail=Ciao ^
  -F typeBody=1 ^
  -F files=@C:\\path\\to\\prova.txt
```

> Nota: più allegati sono supportati ripetendo `-F files=@...`.