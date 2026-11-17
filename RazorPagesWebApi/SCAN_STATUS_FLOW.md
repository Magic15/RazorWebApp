# Architektura GUI i API

Rozwiązanie składa się z dwóch niezależnych projektów:

- `RazorPagesWebApp` renderuje GUI i komunikuje się z backendem przez HTTP.
- `RazorPagesWebApi` udostępnia REST API, obsługuje skanowanie i przechowuje dane w SQLite.

Frontend nie ma referencji do projektu backendu ani pakietów bazodanowych. Adres API ustawia `BackendApi:BaseUrl` w konfiguracji GUI. Backend ogranicza CORS do originów z `Cors:AllowedOrigins`.

Lokalnie uruchom oba projekty: GUI pod `http://localhost:5238`, API pod `http://localhost:5280`. W Riderze można ustawić oba projekty jako projekty startowe rozwiązania. Jeśli zmienisz host lub port GUI, dodaj jego origin do `Cors:AllowedOrigins`; jeśli zmienisz adres API, ustaw `BackendApi:BaseUrl` w konfiguracji GUI. Dla profilu HTTPS GUI ma nadpisanie `BackendApi__BaseUrl=https://localhost:7280`.

## Model danych

Tabela `ScannedDocuments` symuluje zewnętrzny skaner: przechowuje plik jako BLOB, nazwę, typ MIME i status. Tabela `Documents` przechowuje metadane biznesowe i `ThirdPartyDocumentId` — identyfikator rekordu po stronie skanera.

```mermaid
erDiagram
    SCANNED_DOCUMENTS ||--|| DOCUMENTS : "ThirdPartyDocumentId"
    SCANNED_DOCUMENTS {
        long Id PK
        string FileName
        string ContentType
        blob Data
        string Status
        datetime UpdatedAt
    }
    DOCUMENTS {
        long Id PK
        string FileName
        string DocumentType
        long ThirdPartyDocumentId FK
        datetime UploadedAt
    }
```

Statusy to `InProgress`, `Approved` i `Rejected`.

## Diagram sekwencji

```mermaid
sequenceDiagram
    actor User as Użytkownik
    participant Browser as Przeglądarka
    participant Frontend as Razor Pages GUI
    participant API as RazorPagesWebApi
    participant Documents as DocumentStore
    participant Scanner as ScannedDocumentStore
    participant DB as SQLite
    participant Hub as ScanStatusHub

    User->>Browser: Otwiera GUI
    Browser->>Frontend: GET /
    Frontend->>API: GET /api/documents
    API->>Documents: ListAsync()
    Documents->>DB: SELECT dokumentów i statusów
    DB-->>Frontend: Lista JSON
    Frontend-->>Browser: Strona dokumentów

    User->>Browser: Wysyła plik i typ
    Browser->>API: POST /api/scanned-documents (multipart/form-data)
    API->>Scanner: UploadAsync(nazwa, MIME, bytes)
    Scanner->>DB: INSERT pliku ze statusem InProgress
    DB-->>API: ThirdPartyDocumentId
    API-->>Browser: HTTP 201 + ThirdPartyDocumentId
    Browser->>API: POST /api/documents (metadane + ThirdPartyDocumentId)
    API->>Documents: CreateAsync(metadane, ThirdPartyDocumentId)
    Documents->>DB: INSERT metadanych
    API-->>Browser: HTTP 201

    Browser->>API: SignalR negotiate + WebSocket /hubs/scan-status
    API-->>Browser: Połączenie SignalR

    User->>API: PUT /api/scanned-documents/{externalId}/status
    API->>Scanner: UpdateStatusAsync(externalId, status)
    Scanner->>DB: UPDATE statusu
    API->>Hub: Clients.All.SendAsync(scanStatusChanged, ...)
    Hub-->>Browser: SignalR scanStatusChanged
    Browser->>Browser: Aktualizuje status w tabeli

    User->>Browser: Pobiera zatwierdzony dokument
    Browser->>API: GET /api/documents/{id}/file
    API->>Documents: Pobiera external ID
    API->>Scanner: Odczytuje plik i status
    alt Status Approved
        API-->>Browser: Plik
    else InProgress lub Rejected
        API-->>Browser: HTTP 409
    end
```

## Endpointy

- `GET /api/documents` — lista dokumentów i statusów.
- `POST /api/scanned-documents` — upload pliku do symulowanego skanera; odpowiedź zawiera nowe `thirdPartyDocumentId`.
- `GET /api/scanned-documents/{thirdPartyDocumentId}` — metadane i status pliku u skanera.
- `GET /api/scanned-documents/{thirdPartyDocumentId}/file` — plik tylko po statusie `Approved`.
- `PUT /api/scanned-documents/{thirdPartyDocumentId}/status` — aktualizacja statusu.
- `POST /api/documents` — zapis metadanych GUI wraz z zewnętrznym ID.
- `GET /api/documents/{id}` — szczegóły lokalnego dokumentu.
- `GET /api/documents/{id}/file` — pobranie przez lokalne ID; backend sprawdza status u skanera.
- `/hubs/scan-status` — SignalR Hub, który wysyła zdarzenie `scanStatusChanged` do podłączonych klientów po zmianie statusu.

Formularz GUI wysyła plik i metadane bezpośrednio do REST API przez `fetch`. Adres backendu jest konfigurowalny; dla CORS trzeba dodać origin GUI do `Cors:AllowedOrigins` po stronie backendu.

GUI łączy się z SignalR przez współdzielony `ScanStatusEventsViewComponent` w `RazorPagesWebApp`. Dowolna strona Razor może dodać `@await Component.InvokeAsync("ScanStatusEvents")`; wspólny `scan-status-events.js` otworzy połączenie WebSocket z hubem i wyemituje zdarzenie DOM `scan-status-changed`. Tabela może opcjonalnie oznaczyć wiersz `data-third-party-document-id`, komórkę `.scan-status` i komórkę akcji `.scan-status-action`, aby skrypt aktualizował status i przycisk pobierania.

Klient SignalR jest skonfigurowany wyłącznie do WebSocketów, aby nie przechodził na transport SSE, który może być buforowany przez IIS. Na IIS należy włączyć funkcję **WebSocket Protocol**. CORS dla huba musi zezwalać na dokładne originy GUI i obsługiwać credentials. Przy wielu instancjach API trzeba skonfigurować SignalR backplane (np. Redis) i odpowiednią konfigurację load balancera; domyślny hub przechowuje połączenia w pamięci pojedynczej instancji.

Requesty API są walidowane przez osobne klasy FluentValidation w katalogu `Validation`. Błędy zwracane są jako `ValidationProblemDetails`. Plik `Requests/ScanDocument.http` zawiera przykładowe requesty do Ridera.

## Ograniczenia symulacji

- Backend przechowuje skaner w tej samej bazie SQLite wyłącznie na potrzeby symulacji. Prawdziwa integracja zastąpiłaby `ScannedDocumentStore` klientem HTTP zewnętrznego skanera.
- Połączenia SignalR są zarządzane w pamięci pojedynczej instancji; wiele instancji wymaga backplane, np. Redis.
- Endpoint zmiany statusu nie ma uwierzytelniania ani podpisu callbacku.
