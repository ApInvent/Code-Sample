using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CustIS.NTier.Common.RemoteQuery;

namespace CustIS.NTier.Client
{
    public class AppTask<T>
    {
        private readonly ErrorService _errorService;
        private readonly List<PropertySetter> _blockablePropeties = new List<PropertySetter>();
        private readonly List<IBlockable> _blockables = new List<IBlockable>();
        private IAppTaskHost _host;
        private Func<Task<T>> _taskFunc;
        private Action<T> _executeOnCompleted;
        private Action<Exception> _executeOnException;
        private Action _executeOnCanceled;
        private bool _showException = true;
        private Timer _addToTasksTimer;

        public AppTask(ErrorService errorService)
        {
            _errorService = errorService;
        }

        public AppTask<T> Block(params Expression<Func<bool>>[] lambdas)
        {
            foreach (var lambda in lambdas)
            {
                Func<object> propertyOwner;
                var propertyInfo = lambda.ParseLambda(out propertyOwner);
                _blockablePropeties.Add(new PropertySetter(propertyInfo, propertyOwner));
            }
            return this;
        }

        public AppTask<T> Block(params IBlockable[] blockables)
        {
            _blockables.AddRange(blockables);
            return this;
        }

        public AppTask<T> BlockHost()
        {
            if (_host != null)
                Block(_host as IBlockable);
            return this;
        }

        public AppTask<T> OnCanceled(Action onCanceled)
        {
            _executeOnCanceled = onCanceled;
            return this;
        }

        public AppTask<T> OnException(Action<Exception> onException, bool replace = true)
        {
            _executeOnException = onException;
            _showException = !replace;
            return this;
        }

        public AppTask<T> OnComplete(Action<T> onComplete)
        {
            _executeOnCompleted = onComplete;
            return this;
        }

        public AppTask<T> Description(string description)
        {
            DisplayTitle = description;
            return this;
        }

        public AppTask<T> Action(Func<Task<T>> task, CancellationTokenSource tokenSource = null)
        {
            _taskFunc = task;
            CancellationTokenSource = tokenSource ?? new CancellationTokenSource();
            return this;
        }

        public AppTask<T> Cancellable()
        {
            CanCancel = true;
            return this;
        }

        public AppTask<T> Host(IAppTaskHost host)
        {
            _host = host;
            return this;
        }

        public void Execute()
        {
            var task = GetTask();
            if (null == task)
                return;

            BeforeExecute();

            task.ContinueWith(t =>
            {
                if (CancellationTokenSource.IsCancellationRequested)
                    return;

                HandleTaskResult(task);
            });
        }

        private void AddToTasksCallback(object state)
        {
            if (IsComplete) return;
            lock (_addToTasksTimer)
                if (!IsComplete && null != _host)
                    _host.Tasks.Add(this);
        }

        private Task<T> GetTask()
        {
            try
            {
                var task = _taskFunc();
                if (task.Status == TaskStatus.Created)
                    task.Start();
                return task;
            }
            catch (Exception e)
            {
                OnException(e);
            }

            return null;
        }

        private void HandleTaskResult(Task<T> task)
        {
            AfterExecute();

            if (task.IsFaulted || task.Exception != null)
            {
                if (task.Exception.InnerExceptions.Count > 1)
                    OnException(task.Exception);
                else
                    OnException(task.Exception.InnerExceptions.Single());
            }
            else if (task.IsCompleted && !task.IsCanceled && _executeOnCompleted != null)
            {
                try
                {
                    _executeOnCompleted(task.Result);
                }
                catch (Exception e)
                {
                    OnException(e);
                }
            }
            else if (task.IsCompleted && task.IsCanceled && _executeOnCanceled != null)
            {
                try
                {
                    _executeOnCanceled();
                }
                catch (Exception e)
                {
                    OnException(e);
                }
            }
        }

        public void Cancel()
        {
            if (!CanCancel)
                return;

            CancellationTokenSource.Cancel();
            AfterExecute();
            if (_executeOnCanceled != null)
                _executeOnCanceled();
        }

        private void BeforeExecute()
        {
            foreach (var blockable in _blockables)
                blockable.Block();
            foreach (var propertySetter in _blockablePropeties)
                propertySetter.Set(false);

            IsComplete = false;
            const int delayTimeBeforeAddToTasks = 500;
            _addToTasksTimer = new Timer(AddToTasksCallback, this, delayTimeBeforeAddToTasks, Timeout.Infinite);
        }

        private void AfterExecute()
        {
            lock (_addToTasksTimer)
                IsComplete = true;
            if (null != _host)
                _host.Tasks.Remove(this);
            foreach (var blockable in _blockables)
                blockable.Unblock();
            foreach (var propertySetter in _blockablePropeties)
                propertySetter.Set(true);
        }

        protected virtual void OnException(Exception ex)
        {
            if (_showException)
                _errorService.ShowException(ex);
            var handler = _executeOnException;
            if (handler != null) handler(ex);
        }

        public bool CanCancel { get; protected set; }
        public string DisplayTitle { get; protected set; }
        private CancellationTokenSource CancellationTokenSource { get; set; }
        private bool IsComplete { get; set; }

        private struct PropertySetter
        {
            private readonly Func<object> _propertyOwner;
            private readonly PropertyInfo _propertyInfo;

            public PropertySetter(PropertyInfo propertyInfo, Func<object> propertyOwner)
            {
                _propertyInfo = propertyInfo;
                _propertyOwner = propertyOwner;
            }

            public void Set(bool value)
            {
                _propertyInfo.SetValue(_propertyOwner(), value, null);
            }
        }
    }

    public class AppTaskFactory
    {
        private readonly ErrorService _errorService;
        private IAppTaskHost _host;

        public AppTaskFactory(ErrorService errorService)
        {
            _errorService = errorService;
        }

        public AppTask<T> Create<T>()
        {
            var appTask = new AppTask<T>(_errorService);
            if (_host != null)
                appTask.Host(_host);
            return appTask;
        }

        public AppTask<T> FromTask<T>(Func<Task<T>> task, CancellationTokenSource tokenSource = null)
        {
            return Create<T>().Action(task, tokenSource);
        }

        public AppTask<bool> FromTask(Func<Task> task, CancellationTokenSource tokenSource = null)
        {
            return Create<bool>().Action(() => task().ContinueWith(prevTask => true, TaskContinuationOptions.OnlyOnRanToCompletion), tokenSource);
        }

        public AppTaskFactory Host(IAppTaskHost host)
        {
            _host = host;
            return this;
        }
    }
}