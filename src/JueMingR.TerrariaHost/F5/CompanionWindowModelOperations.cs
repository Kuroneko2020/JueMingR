using System;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Platform.Llm;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed partial class CompanionWindow
    {
        private bool modelDiscoveryPickerPending;

        private void ModelMessage(string value)
        {
            modelStatus = value;
            contentInvalidated = true;
        }

        private void ModelFailure(string summary, string details)
        {
            modelTestDetails = details;
            modelTestDetailsExpanded = true;
            ModelMessage(summary);
        }

        private void StartModelWrite(Func<Task> write, string message, Action completed = null)
        {
            if (modelWrite != null) return;
            modelWriteCompleted = completed;
            modelWrite = FinishModelWrite(write);
            ModelMessage("正在保存设置…");
            modelWriteSuccess = message;
        }

        private string modelWriteSuccess;

        private static async Task<string> FinishModelWrite(Func<Task> write)
        {
            try { await write().ConfigureAwait(false); return null; }
            catch (LlmException exception) { return exception.Message; }
            catch (Exception) { return "暂时无法保存设置，请检查配置页中的状态。"; }
        }

        private void StartModelDiscovery()
        {
            var draft = modelDraft;
            var connections = modelConnections;
            StartModelRead("models", "正在获取模型…", async token =>
            {
                var entries = await connections.DiscoverAsync(draft, token).ConfigureAwait(false);
                return new ModelReadResult
                {
                    Success = true, Models = entries,
                    Message = entries.Count == 0 ? "服务返回空列表。可以从控制台核对模型 ID 后手动填写。" : "已获取 " + entries.Count + " 个模型。"
                };
            });
        }

        private void StartModelTest(bool tools)
        {
            var draft = modelDraft;
            var connections = modelConnections;
            string label = ModelTestSubject(draft, modelPreset);
            StartModelRead(tools ? "tools" : "chat", tools ? "正在测试工具调用…" : "正在测试连接…", async token =>
            {
                LlmConnectionTest result = tools ? await connections.TestToolsAsync(draft, token).ConfigureAwait(false) :
                    await connections.TestChatAsync(draft, token).ConfigureAwait(false);
                return new ModelReadResult { Success = result.Success, Message = result.Description };
            }, label);
        }

        private void StartModelRead(string kind, string message, Func<CancellationToken, Task<ModelReadResult>> read, string testSubject = null)
        {
            if (modelRead != null || modelWrite != null) return;
            modelTestDetails = null;
            modelTestDetailsExpanded = false;
            modelCancellation = new CancellationTokenSource();
            modelReadKind = kind;
            modelRead = FinishModelRead(read, modelCancellation.Token, testSubject);
            ModelMessage(message);
        }

        private static async Task<ModelReadResult> FinishModelRead(Func<CancellationToken, Task<ModelReadResult>> read,
            CancellationToken token, string testSubject)
        {
            ModelReadResult result;
            try { result = await read(token).ConfigureAwait(false); }
            catch (OperationCanceledException) { result = new ModelReadResult { Message = "请求已取消。" }; }
            catch (LlmException exception) { result = new ModelReadResult { Message = exception.Message }; }
            catch (Exception) { result = new ModelReadResult { Message = "暂时无法取得服务结果。" }; }
            if (testSubject != null)
            {
                string time = DateTimeOffset.Now.ToString("MM-dd HH:mm");
                result.Detail = testSubject + "\n" + time + "\n" + result.Message;
                result.Message = (result.Success ? "测试通过 · " : "未通过 · ") + time;
            }
            return result;
        }

        private void PollModelOperations()
        {
            if (modelWrite?.IsCompleted == true)
            {
                string error = modelWrite.GetAwaiter().GetResult();
                modelWrite = null;
                var completed = modelWriteCompleted;
                modelWriteCompleted = null;
                if (error == null)
                {
                    RefreshModelList();
                    completed?.Invoke();
                }
                if (error == null) ModelMessage(modelWriteSuccess);
                else ModelFailure("保存未完成", error);
                modelWriteSuccess = null;
            }
            if (modelRead?.IsCompleted != true)
            {
                ShowDiscoveredModels();
                return;
            }
            ModelReadResult result = modelRead.GetAwaiter().GetResult();
            string kind = modelReadKind;
            modelRead = null;
            modelReadKind = null;
            modelCancellation.Dispose(); modelCancellation = null;
            // Only the currently shown draft can receive these results. A
            // profile selection, newer request or credential edit retires the
            // captured task, even where the transport finished concurrently.
            if (kind == "models" && result.Success)
            {
                if (!modelConnections.HasValidDiscovery(modelDraft))
                {
                    ModelMessage("连接已变化，请重新获取模型。");
                    return;
                }
                discoveredModels = result.Models;
                modelDiscoveryPickerPending = discoveredModels.Count > 0;
            }
            if (result.Detail != null)
            {
                modelTestDetails = result.Detail;
                modelTestDetailsExpanded = !result.Success;
            }
            if (kind == "models" && !result.Success) ModelFailure("未能获取模型", result.Message);
            else ModelMessage(result.Message);
            ShowDiscoveredModels();
        }

        private void ShowDiscoveredModels()
        {
            // Let the existing editor or dropdown finish before switching views.
            // Connections retire their own list; merely choosing a model never
            // changes the endpoint, credential or account that returned it.
            if (!modelDiscoveryPickerPending || modelRead != null || page != ModelPage || modelPageView != ModelPageView.Setup ||
                modelPicker != ModelPicker.None || ModelDropdownOpen || aiInput.Editor != null) return;
            modelDiscoveryPickerPending = false;
            var only = discoveredModels.Count == 1 ? discoveredModels[0] : null;
            if (only != null && HasModelCapability(only.Capability, "文字") && String.IsNullOrWhiteSpace(modelDraft.Model))
                modelDraft.Model = only.Id;
            OpenModelPicker(ModelPicker.Models);
        }

        private void CancelModelRequest()
        {
            var task = modelRead;
            var cancellation = modelCancellation;
            modelRead = null;
            modelReadKind = null;
            modelCancellation = null;
            if (cancellation == null) return;
            cancellation.Cancel();
            if (task.IsCompleted) cancellation.Dispose();
            else _ = task.ContinueWith(_ => cancellation.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private void RetireModelRead()
        {
            InvalidateModelTests();
            discoveredModels = null;
            modelDiscoveryPickerPending = false;
        }

        private void InvalidateModelTests()
        {
            CancelModelRequest();
            modelTestDetails = null;
            modelTestDetailsExpanded = false;
        }
    }
}
