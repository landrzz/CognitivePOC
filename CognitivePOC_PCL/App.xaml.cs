using Acr.UserDialogs;
using CognitivePOC_PCL.PageModels;
using FreshMvvm;
using Xamarin.Forms;

namespace CognitivePOC_PCL
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            SetupIOC();

            SetupMain();
        }


        void SetupIOC()
        {
            FreshMvvm.FreshIOC.Container.Register<IUserDialogs>(UserDialogs.Instance);
        }

        void SetupMain()
        {
            var firstPage = FreshPageModelResolver.ResolvePageModel<CognitivePOC_PCLPageModel>();
            var navContainer = new FreshNavigationContainer(firstPage);
            MainPage = navContainer;

        }

        protected override void OnStart()
        {
            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}
