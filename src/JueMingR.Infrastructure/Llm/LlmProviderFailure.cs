using System.Globalization;
using JueMingR.Platform.Llm;
using static JueMingR.Infrastructure.Llm.LlmJson;

namespace JueMingR.Infrastructure.Llm
{
    internal static class LlmProviderFailure
    {
        public static LlmException Classify(object document, int? status = null)
        {
            object error = Get(document, "error") ?? document;
            object rawCode = Get(error, "code") ?? Get(error, "type");
            string code = rawCode as string ?? (rawCode is int n ? n.ToString(CultureInfo.InvariantCulture) : "");
            // Only recognized machine codes cross the classification boundary. A remote message may echo request secrets.
            switch (code.ToLowerInvariant())
            {
                case "insufficient_balance": case "arrearage": case "accountoverdueerror": case "operationdenied.serviceoverdue":
                    return Error(LlmFailure.Balance, "当前连接的账户余额不足或有欠费。请查看这个账户的计费控制台。", status);
                case "insufficient_quota": case "billing_hard_limit_reached": case "allocationquota.freetieronly":
                case "quotaexceeded": case "accountquotaexceeded": case "quotaexceeded.agentplanquotaexceeded":
                case "setlimitexceeded":
                    return Error(LlmFailure.Quota, "当前方案的可用额度不足或已达到用量上限。", status);
                case "subscription_expired": case "plan_expired":
                    return Error(LlmFailure.PlanExpired, "当前计费方案已到期。请查看这个账户的套餐状态。", status);
                case "rate_limit_exceeded": case "rate_limit_error": case "limit_requests": case "limit_burst_rate":
                case "throttling.ratequota": case "throttling.burstrate": case "throttling.concurrency":
                case "account_ratelimit_exceeded": case "accountratelimitexceeded": case "apiaccountrpmratelimitexceeded":
                case "modelaccountconcurrentrequestslimitexceeded": case "requestbursttoofast":
                    return Error(LlmFailure.RateLimited, "当前连接的请求频率或并发触发限流，请稍后重试。", status);
                case "model_not_found": case "modelnotfound": case "modelnotopen": case "unsupportedmodel":
                case "operationdenied.permissiondenied":
                    return Error(LlmFailure.ModelUnavailable, "当前连接无权使用该模型，或模型 ID 不存在。", status);
                case "authentication_error": case "invalid_api_key": case "permission_error":
                    return Error(LlmFailure.Authentication, "服务拒绝了当前连接的凭据。", status);
                case "serveroverloaded": case "overloaded_error":
                    return Error(LlmFailure.Service, "模型服务当前繁忙，请稍后重试。", status);
                default: return null;
            }
        }

        private static LlmException Error(LlmFailure failure, string message, int? status) =>
            new LlmException(failure, message, status);
    }
}
