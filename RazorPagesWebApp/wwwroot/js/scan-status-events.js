document.addEventListener('DOMContentLoaded', () => {
    if (!window.EventSource) return;

    const eventSources = new Map();
    document.querySelectorAll('[data-scan-status-events]').forEach(component => {
        const apiBaseUrl = component.dataset.apiBaseUrl;
        if (eventSources.has(apiBaseUrl)) return;

        const events = new EventSource(component.dataset.eventsUrl);
        eventSources.set(apiBaseUrl, events);

        events.addEventListener('scan-status', event => {
            const update = JSON.parse(event.data);
            const matchingRows = document.querySelectorAll('[data-third-party-document-id]');

            matchingRows.forEach(row => {
                if (row.dataset.thirdPartyDocumentId !== String(update.thirdPartyDocumentId)) return;

                const statusCell = row.querySelector('.scan-status');
                const actionCell = row.querySelector('.scan-status-action');
                if (!statusCell || !actionCell) return;

                statusCell.textContent = update.status;
                if (update.status === 'Approved') {
                    const link = document.createElement('a');
                    link.className = 'btn btn-sm btn-outline-secondary';
                    link.href = row.dataset.downloadUrl;
                    link.textContent = 'Pobierz';
                    actionCell.replaceChildren(link);
                } else {
                    const hint = document.createElement('span');
                    hint.className = 'text-muted';
                    hint.textContent = 'Dostępny po akceptacji';
                    actionCell.replaceChildren(hint);
                }
            });

            document.dispatchEvent(new CustomEvent('scan-status-changed', { detail: update }));
        });
    });
});
