using UserControl = System.Windows.Controls.UserControl;

namespace FanShop.ViewModels;

public class TabItem : BaseViewModel
{
    private string _title;
    private UserControl _content;
    private bool _isClosable = true;
    
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    
    public UserControl Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }
    
    public bool IsClosable
    {
        get => _isClosable;
        set => SetProperty(ref _isClosable, value);
    }
}