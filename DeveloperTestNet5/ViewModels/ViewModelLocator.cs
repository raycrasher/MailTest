using System;
using System.Collections.Generic;
using System.Text;
using DeveloperTestNet5.i18n;
using Microsoft.Extensions.DependencyInjection;

namespace DeveloperTestNet5.ViewModels
{
    public class ViewModelLocator
    {
        public MainWindowViewModel MainWindowViewModel => App.ServiceProvider.GetService<MainWindowViewModel>();
        public TranslationSource TranslationSource => App.ServiceProvider.GetService<TranslationSource>();
    }
}
