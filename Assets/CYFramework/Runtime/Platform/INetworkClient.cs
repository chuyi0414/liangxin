// ============================================================================
// CYFramework - 网络客户端接口
// 平台抽象层：封装 HTTP 和 WebSocket 的平台差异
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CYFramework.Runtime.Platform
{
    /// <summary>
    /// HTTP 请求方法
    /// </summary>
    public enum HttpMethod
    {
        GET,
        POST,
        PUT,
        DELETE
    }

    /// <summary>
    /// HTTP 响应
    /// </summary>
    public class HttpResponse
    {
        public bool IsSuccess { get; set; }
        public long StatusCode { get; set; }
        public string Body { get; set; }
        public string Error { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }

    /// <summary>
    /// 网络客户端接口
    /// </summary>
    public interface INetworkClient
    {
        /// <summary>
        /// 发送 HTTP 请求
        /// </summary>
        void SendRequest(string url, HttpMethod method, string body, 
            Dictionary<string, string> headers, Action<HttpResponse> callback);

        /// <summary>
        /// GET 请求
        /// </summary>
        void Get(string url, Action<HttpResponse> callback);

        /// <summary>
        /// POST 请求
        /// </summary>
        void Post(string url, string jsonBody, Action<HttpResponse> callback);

        /// <summary>
        /// 下载文件
        /// </summary>
        void DownloadFile(string url, string savePath, Action<bool, float> progressCallback);

        /// <summary>
        /// 取消所有请求
        /// </summary>
        void CancelAll();
    }

    /// <summary>
    /// Unity 平台的网络客户端实现
    /// </summary>
    public class UnityNetworkClient : INetworkClient
    {
        private readonly List<UnityWebRequest> _activeRequests = new List<UnityWebRequest>();

        public void SendRequest(string url, HttpMethod method, string body,
            Dictionary<string, string> headers, Action<HttpResponse> callback)
        {
            Core.CYFrameworkEntry.Instance.StartCoroutine(
                SendRequestCoroutine(url, method, body, headers, callback));
        }

        private IEnumerator SendRequestCoroutine(string url, HttpMethod method, string body,
            Dictionary<string, string> headers, Action<HttpResponse> callback)
        {
            UnityWebRequest request;

            switch (method)
            {
                case HttpMethod.GET:
                    request = UnityWebRequest.Get(url);
                    break;
                case HttpMethod.POST:
                    request = new UnityWebRequest(url, "POST");
                    if (!string.IsNullOrEmpty(body))
                    {
                        byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    }
                    request.downloadHandler = new DownloadHandlerBuffer();
                    break;
                case HttpMethod.PUT:
                    request = UnityWebRequest.Put(url, body);
                    break;
                case HttpMethod.DELETE:
                    request = UnityWebRequest.Delete(url);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    break;
                default:
                    request = UnityWebRequest.Get(url);
                    break;
            }

            // 设置请求头
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.SetRequestHeader(header.Key, header.Value);
                }
            }

            // 默认设置 JSON 内容类型
            if (method == HttpMethod.POST || method == HttpMethod.PUT)
            {
                request.SetRequestHeader("Content-Type", "application/json");
            }

            _activeRequests.Add(request);

            yield return request.SendWebRequest();

            _activeRequests.Remove(request);

            HttpResponse response = new HttpResponse
            {
                StatusCode = request.responseCode,
                Headers = new Dictionary<string, string>()
            };

            if (request.result == UnityWebRequest.Result.Success)
            {
                response.IsSuccess = true;
                response.Body = request.downloadHandler?.text;
            }
            else
            {
                response.IsSuccess = false;
                response.Error = request.error;
            }

            // 获取响应头
            var responseHeaders = request.GetResponseHeaders();
            if (responseHeaders != null)
            {
                foreach (var header in responseHeaders)
                {
                    response.Headers[header.Key] = header.Value;
                }
            }

            request.Dispose();
            callback?.Invoke(response);
        }

        public void Get(string url, Action<HttpResponse> callback)
        {
            SendRequest(url, HttpMethod.GET, null, null, callback);
        }

        public void Post(string url, string jsonBody, Action<HttpResponse> callback)
        {
            SendRequest(url, HttpMethod.POST, jsonBody, null, callback);
        }

        public void DownloadFile(string url, string savePath, Action<bool, float> progressCallback)
        {
            Core.CYFrameworkEntry.Instance.StartCoroutine(
                DownloadFileCoroutine(url, savePath, progressCallback));
        }

        private IEnumerator DownloadFileCoroutine(string url, string savePath, 
            Action<bool, float> progressCallback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerFile(savePath);
                
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    progressCallback?.Invoke(false, request.downloadProgress);
                    yield return null;
                }

                bool success = request.result == UnityWebRequest.Result.Success;
                progressCallback?.Invoke(true, success ? 1f : 0f);

                if (!success)
                {
                    Debug.LogError($"[NetworkClient] 下载失败: {request.error}");
                }
            }
        }

        public void CancelAll()
        {
            foreach (var request in _activeRequests)
            {
                request?.Abort();
                request?.Dispose();
            }
            _activeRequests.Clear();
        }
    }
}
