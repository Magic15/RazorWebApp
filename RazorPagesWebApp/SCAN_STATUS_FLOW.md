# Przechowywanie i status skanowania dokumentów

## Model danych

W tej symulacji tabela `ScannedDocuments` reprezentuje system third party: zawiera fizyczny plik jako BLOB, nazwę pliku, typ MIME i status skanowania. Nasza tabela `Documents` przechowuje metadane biznesowe, takie jak typ dokumentu, oraz `ThirdPartyDocumentId` — identyfikator pliku w systemie skanera.

Schemat relacji:

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

Statusy to `InProgress`, `Approved` i `Rejected`. Po wgraniu pliku tworzony jest rekord po stronie skanera ze statusem `InProgress`, a jego ID zostaje zapisane w `Documents.ThirdPartyDocumentId`.

## Diagram sekwencji

```mermaid
sequenceDiagram
    actor User as Użytkownik
    participant Browser as Przeglądarka
    participant Page as Razor Pages
    participant API as ScannedDocumentsController
    participant Store as DocumentStore
    participant DB as SQLite (symulacja third party)
    participant Events as ScanStatusEvents
    actor Scanner as Skaner / Rider .http

    User->>Browser: Wgrywa plik i typ dokumentu
    Browser->>Page: POST formularza multipart
    Page->>Store: SaveAsync(metadane, bytes)
    Store->>DB: INSERT pliku do ScannedDocuments (InProgress)
    DB-->>Store: ThirdPartyDocumentId
    Store->>DB: INSERT metadanych i ThirdPartyDocumentId do Documents
    Store-->>Page: Zapis zakończony
    Page-->>Browser: Przekierowanie na listę

    Browser->>Page: GET /?handler=StatusEvents
    Page->>Events: Subscribe()
    Page-->>Browser: Otwarty strumień SSE

    Scanner->>API: POST /api/scanned-documents/{thirdPartyDocumentId}/status
    API->>API: Waliduje status
    API->>Store: UpdateScanStatusAsync(thirdPartyDocumentId, status)
    Store->>DB: UPDATE ScannedDocuments SET Status, UpdatedAt
    DB-->>Store: Rekord zaktualizowany
    Store-->>API: Sukces
    API->>Events: Publish(thirdPartyDocumentId, status)
    Events-->>Page: Zdarzenie w kanale SSE
    Page-->>Browser: event scan-status
    Browser->>Browser: Aktualizuje status i przycisk pobierania

    User->>Browser: Wybiera Pobierz (jeśli Approved)
    Browser->>Page: GET /?handler=Download&id={localDocumentId}
    Page->>Store: GetDownloadAsync(localDocumentId)
    Store->>DB: Pobierz plik i sprawdź status
    alt Status Approved
        DB-->>Page: Plik
        Page-->>Browser: Pobranie pliku
    else InProgress lub Rejected
        DB-->>Page: Status niezatwierdzony
        Page-->>Browser: HTTP 403
    end
```

## Endpoint zmiany statusu

Skaner wysyła zewnętrzny identyfikator — nie lokalne `Documents.Id`:

```http
POST http://localhost:5238/api/scanned-documents/3/status
Content-Type: application/json

{"status":"Approved"}
```

Plik `Requests/ScanDocument.http` zawiera przykłady requestów do Ridera. Wartość zmiennej `@thirdPartyDocumentId` musi wskazywać `Documents.ThirdPartyDocumentId`.

API przyjmuje tylko trzy wymienione statusy. Nieprawidłowy status zwraca `400`, a nieznany zewnętrzny identyfikator — `404`. Po udanej zmianie status jest publikowany do otwartych stron przez Server-Sent Events (SSE).

## Dostęp do pliku

Widok pokazuje przycisk pobierania tylko dla statusu `Approved`. Handler pobierania ponownie sprawdza status w bazie, więc ręczne wywołanie URL dla pliku `InProgress` lub `Rejected` zwraca `403`. Plik jest odczytywany z `ScannedDocuments`, a nie z tabeli `Documents`.

## Inicjalizacja bazy

Przy starcie `DatabaseInitializer` tworzy bazę oraz tabele `ScannedDocuments` i `Documents`, jeśli jeszcze nie istnieją. Aplikacja zakłada świeży schemat i nie próbuje migrować ani odtwarzać danych ze starszego układu tabel.

## Ograniczenia symulacji

- Tabela `ScannedDocuments` znajduje się w tej samej bazie SQLite, tylko po to, by zasymulować zewnętrzny system. W rzeczywistej integracji `DocumentStore` wywoływałby API third party, a plik nie byłby lokalnym BLOB-em.
- Powiadomienia SSE istnieją w pamięci procesu i nie są odtwarzane po rozłączeniu. Po odświeżeniu strony status jest pobierany ponownie z bazy.
- Rozwiązanie SSE działa dla jednej instancji aplikacji. Wiele instancji wymagałoby współdzielonego brokera zdarzeń.
- Endpoint callbacku nie ma uwierzytelniania ani podpisu requestu; jest przeznaczony do lokalnej symulacji.
