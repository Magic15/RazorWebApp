document.addEventListener('DOMContentLoaded', () => {
    if (!window.signalR) {
        console.error('SignalR client library was not loaded.');
        return;
    }

    const connections = new Map();

    document.querySelectorAll('[data-scan-status-events]').forEach(component => {
        const hubUrl = component.dataset.hubUrl;
        if (!hubUrl || connections.has(hubUrl)) return;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, { transport: signalR.HttpTransportType.WebSockets })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();

        connections.set(hubUrl, connection);

        connection.on('scanStatusChanged', update => {
            console.info('[SignalR] Odebrano scanStatusChanged:', update);
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

        connection.onreconnecting(error => {
            console.warn('[SignalR] Ponawianie połączenia:', error);
        });

        connection.onreconnected(connectionId => {
            console.info('[SignalR] Połączenie przywrócone:', connectionId);
        });

        connection.onclose(error => {
            console.error('[SignalR] Połączenie zamknięte:', error);
            void startConnection(connection);
        });

        void startConnection(connection);
    });

    async function startConnection(connection) {
        while (connection.state === signalR.HubConnectionState.Disconnected) {
            try {
                await connection.start();
                console.info('[SignalR] Połączono:', connection.connectionId);
                return;
            } catch (error) {
                console.error('[SignalR] Nie udało się połączyć; ponowię za 5 sekund:', error);
                await new Promise(resolve => setTimeout(resolve, 5000));
            }
        }
    }
});
