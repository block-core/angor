window.webSocketInterop = {
    webSocket: null,
    connect: function (url, dotNetObjectRef) {
        if (this.webSocket) {
            console.warn("WebSocket is already connected.");
            return;
        }

        this.webSocket = new WebSocket(url);

        this.webSocket.onopen = function () {
            console.log("WebSocket connected.");
            dotNetObjectRef.invokeMethodAsync('OnWebSocketOpen');
        };

        this.webSocket.onmessage = function (event) {
            // console.log("WebSocket message received:", event.data);
            dotNetObjectRef.invokeMethodAsync('OnWebSocketMessage', event.data);
        };

        this.webSocket.onerror = function (error) {
            console.error("WebSocket error:", error);
            dotNetObjectRef.invokeMethodAsync('OnWebSocketError', error.message);
        };

        this.webSocket.onclose = function () {
            console.log("WebSocket closed.");
            dotNetObjectRef.invokeMethodAsync('OnWebSocketClose');
        };
    },

    disconnect: function () {
        if (this.webSocket) {
            this.webSocket.close();
            this.webSocket = null;
            console.log("WebSocket disconnected.");
        } else {
            console.warn("WebSocket is not connected.");
        }
    },

    sendMessage: function (message) {
        if (this.webSocket && this.webSocket.readyState === WebSocket.OPEN) {
            this.webSocket.send(message);
        } else {
            console.warn("WebSocket is not connected or not open.");
        }
    }
};
