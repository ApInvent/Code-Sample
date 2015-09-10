using Caliburn.Micro;

namespace CustIS.NTier.Client
{
    public interface IAppTaskHost
    {
        BindableCollection<object> Tasks { get; set; }
    }
}