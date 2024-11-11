using System.Windows.Controls;

namespace LumiTracker.Controls
{
    public partial class AvatarsView : UserControl
    {
        public static readonly DependencyProperty CharacterIdsProperty = DependencyProperty.Register(
            "CharacterIds", typeof(IList<int>), typeof(AvatarsView), new PropertyMetadata(new List<int> { -1, -1, -1 } ));

        public IList<int> CharacterIds
        {
            get { return (IList<int>)GetValue(CharacterIdsProperty); }
            set { SetValue(CharacterIdsProperty, value); }
        }

        public static readonly DependencyProperty ImageOuterMarginProperty = DependencyProperty.Register(
            "ImageOuterMargin", typeof(Thickness), typeof(AvatarsView), new PropertyMetadata(new Thickness(0)));

        public Thickness ImageOuterMargin
        {
            get { return (Thickness)GetValue(ImageOuterMarginProperty); }
            set { SetValue(ImageOuterMarginProperty, value); }
        }

        public static readonly DependencyProperty ImageInnerMarginProperty = DependencyProperty.Register(
            "ImageInnerMargin", typeof(Thickness), typeof(AvatarsView), new PropertyMetadata(new Thickness(0)));

        public Thickness ImageInnerMargin
        {
            get { return (Thickness)GetValue(ImageInnerMarginProperty); }
            set { SetValue(ImageInnerMarginProperty, value); }
        }

        public AvatarsView()
        {
            InitializeComponent();
        }
    }
}
