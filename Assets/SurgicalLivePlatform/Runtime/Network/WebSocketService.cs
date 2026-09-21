using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SurgicalLivePlatform.Runtime.Network
{
    public class WebSocketService : IDisposable
    {
        private ClientWebSocket _ws;
        private string _url;
        private CancellationTokenSource _cts;

        private const int MAX_RETRY_COUNT = 5;
        private const int INITIAL_BACKOFF_MS = 1000;

        public event Action<string> OnMessageReceived;
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;

        public async Task ConnectAsync(string url, string token = null)
        {
            _url = url;
            await ConnectWithBackoffAsync(token);
        }

        private async Task ConnectWithBackoffAsync(string token)
        {
            int retryCount = 0;
            int backoffMs = INITIAL_BACKOFF_MS;

            while (retryCount < MAX_RETRY_COUNT)
            {
                try
                {
                    _cts = new CancellationTokenSource();
                    _ws = new ClientWebSocket();
                    
                    if (!string.IsNullOrEmpty(token))
                    {
                        _ws.Options.SetRequestHeader("Authorization", $"Bearer {token}");
                    }

                    Debug.Log($"[WebSocketService] Connecting to {_url} (Attempt {retryCount + 1}/{MAX_RETRY_COUNT})...");
                    
                    // Bypass SSL for local development
                    System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

                    await _ws.ConnectAsync(new Uri(_url), _cts.Token);
                    
                    Debug.Log("[WebSocketService] Connected successfully!");
                    OnConnected?.Invoke();
                    
                    _ = ReceiveLoopAsync();
                    return; // 成功后退出重连循环
                }
                catch (Exception ex)
                {
                    retryCount++;
                    string errorMsg = $"Connection failed: {ex.Message}";
                    Debug.LogWarning($"[WebSocketService] {errorMsg}");
                    OnError?.Invoke(errorMsg);

                    if (retryCount >= MAX_RETRY_COUNT)
                    {
                        Debug.LogError("[WebSocketService] Max retry count reached. Giving up.");
                        OnDisconnected?.Invoke("Max retry count reached");
                        break;
                    }

                    Debug.LogWarning($"[WebSocketService] Waiting {backoffMs}ms before next retry...");
                    await Task.Delay(backoffMs);
                    backoffMs *= 2; // 指数退避
                }
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[4096];

            try
            {
                while (_ws != null && _ws.State == WebSocketState.Open)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.LogWarning("[WebSocketService] Received Close message from server.");
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        OnDisconnected?.Invoke("Server closed connection");
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    OnMessageReceived?.Invoke(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when disconnecting
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocketService] Receive loop exception: {ex.Message}");
                OnDisconnected?.Invoke($"Exception: {ex.Message}");
                
                // 意外断开，触发重连
                Debug.Log("[WebSocketService] Attempting to reconnect due to exception...");
                _ = ConnectWithBackoffAsync(null); 
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
            {
                Debug.LogWarning("[WebSocketService] Cannot send message: WebSocket is not connected.");
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(message);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }

        public async Task DisconnectAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_ws != null)
            {
                if (_ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[WebSocketService] Error during close: {ex.Message}");
                    }
                }
                _ws.Dispose();
                _ws = null;
            }
            
            OnDisconnected?.Invoke("Client disconnected intentionally");
        }

        public void Dispose()
        {
            _ = DisconnectAsync();
        }
    }
}
