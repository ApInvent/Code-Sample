using System;

namespace UsageExample
{
    public class UsageExample
    {
        public void Example1()
        {
            _taskFactory
            .FromTask(() => _agatha.PostAsync<AggrDecodeResponse>(request))
            .Block(() => CanRun, () => CanOpenInExcel)
            .Cancellable()
            .Description("")
            .OnComplete(OnAggrDecodeRequestCompleeted)
            .Execute();
        }

        public void Example2()
        {
            _taskFactory
             .FromTask(() => _repository.SaveAsync(ActionAlgorithm))
             .Description("Сохранение ...")
             .Host(this)
             .BlockHost()
             .OnComplete(result => TryClose())
             .OnException(e => TryClose())
             .Execute();
        }
    }
}