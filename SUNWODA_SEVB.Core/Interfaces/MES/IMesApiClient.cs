using SUNWODA_SEVB.Core.Models.MES;
namespace SUNWODA_SEVB.Core.Interfaces.MES
{
    /// <summary>
    /// MES API客户端接口
    /// </summary>
    public interface IMesApiClient
    {
        /// <summary>
        /// 发送POST请求
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="endpoint"></param>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TResponse> PostAsync<TRequest, TResponse>
        (
            string endpoint,
            TRequest request,
            CancellationToken cancellationToken = default
        )
        where TRequest : IMesRequest
        where TResponse : IMesResponse;

        /// <summary>
        /// 发送POST请求，指定内容类型
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="endpoint"></param>
        /// <param name="request"></param>
        /// <param name="contentType"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TResponse> PostAsync<TRequest, TResponse>
        (
            string endpoint, 
            TRequest request, 
            MesContentType contentType, 
            CancellationToken cancellationToken = default
        )
        where TRequest : IMesRequest
        where TResponse : IMesResponse;

        /// <summary>
        /// 发送GET请求
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="endpoint"></param>
        /// <param name="parameters"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TResponse> GetAsync<TResponse>(
            string endpoint,
            object? parameters = null,
            CancellationToken cancellationToken = default)
            where TResponse : IMesResponse;

    }
}