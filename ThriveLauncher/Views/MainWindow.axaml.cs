using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LauncherBackend.Models;
using LauncherBackend.Utilities;
using ReactiveUI;
using ThriveLauncher.Utilities;
using ThriveLauncher.ViewModels;

namespace ThriveLauncher.Views
{
    public partial class MainWindow : Window
    {
        private readonly List<ComboBoxItem> languageItems = new();
        private bool dataContextReceived;

        public MainWindow()
        {
            InitializeComponent();

            DataContextProperty.Changed.Subscribe(OnDataContextReceiver);
        }

        private void OnDataContextReceiver(AvaloniaPropertyChangedEventArgs e)
        {
            if (dataContextReceived || e.NewValue == null)
                return;

            // Prevents recursive calls
            dataContextReceived = true;

            var dataContext = (MainWindowViewModel)e.NewValue;

            languageItems.AddRange(dataContext.GetAvailableLanguages().Select(l => new ComboBoxItem { Content = l }));

            LanguageComboBox.Items = languageItems;

            dataContext.WhenAnyValue(d => d.SelectedLauncherLanguage).Subscribe(OnLanguageChanged);

            Dispatcher.UIThread.Post(UpdateFeedItemsWhenRetrieved);
        }

        private void SelectedVersionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _ = sender;

            // Ignore while initializing
            if (DataContext == null)
                return;

            if (e.AddedItems.Count != 1 || e.AddedItems[0] == null)
                throw new ArgumentException("Expected one item to be selected");

            var selected = (ComboBoxItem)e.AddedItems[0]!;

            ((MainWindowViewModel)DataContext).VersionSelected((string)selected.Content);
        }

        private void LanguageSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            _ = sender;

            if (e.AddedItems.Count != 1 || e.AddedItems[0] == null)
                throw new ArgumentException("Expected one item to be selected");

            var selected = (ComboBoxItem)e.AddedItems[0]!;

            ((MainWindowViewModel)DataContext!).SelectedLauncherLanguage = (string)selected.Content;
        }

        private void OnLanguageChanged(string selectedLanguage)
        {
            var selected = languageItems.First(i => (string)i.Content == selectedLanguage);

            LanguageComboBox.SelectedItem = selected;
        }

        private void OpenLicensesWindow(object? sender, RoutedEventArgs routedEventArgs)
        {
            _ = sender;
            _ = routedEventArgs;

            var window = new LicensesWindow
            {
                DataContext = this.CreateInstance<LicensesWindowViewModel>(),
            };
            window.Show(this);
        }

        private async void UpdateFeedItemsWhenRetrieved()
        {
            var dataContext = (MainWindowViewModel)DataContext!;

            var devForum = await dataContext.DevForumFeedItems;
            var mainSite = await dataContext.MainSiteFeedItems;

            // TODO: for some reason the items get mixed up
            PopulateFeed(this.FindControl<StackPanel>("DevelopmentFeedItems"), devForum);
            PopulateFeed(this.FindControl<StackPanel>("MainSiteFeedItems"), mainSite);
        }

        private void PopulateFeed(IPanel targetContainer, List<ParsedLauncherFeedItem> items)
        {
            foreach (var child in targetContainer.Children.ToList())
            {
                targetContainer.Children.Remove(child);
            }

            var linkClasses = new Classes("TextLink");

            var lightGrey = new SolidColorBrush((Color?)Application.Current?.Resources["LightGrey"] ??
                throw new Exception("missing brush"));

            // TODO: these items need to be recreated if language changes (or bindings need to be used)
            foreach (var feedItem in items)
            {
                var itemContainer = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                };

                var title = new Button
                {
                    Classes = linkClasses,
                    Content = new TextBlock
                    {
                        Text = feedItem.Title,
                        FontSize = 18,
                        TextWrapping = TextWrapping.Wrap,
                    },
                };

                title.Click += (_, _) => URLUtilities.OpenURLInBrowser(feedItem.Link);

                itemContainer.Children.Add(title);

                var authorAndTime = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,

                    // TODO: a library for x hours ago
                    Text = string.Format(Properties.Resources.FeedItemPostedByAndTime, feedItem.Author,
                        feedItem.PublishedAt),
                    Margin = new Thickness(20, 0, 0, 8),
                    FontSize = 12,
                    Foreground = lightGrey,
                };

                itemContainer.Children.Add(authorAndTime);

                // Main content
                // TODO: this is text and link only for now

                if (feedItem.Truncated)
                {
                    var truncatedContainer = new WrapPanel
                    {
                        Orientation = Orientation.Horizontal,
                    };

                    var truncatedLink = new Button
                    {
                        Classes = linkClasses,
                        Content = Properties.Resources.ClickHereLink,
                    };

                    truncatedLink.Click += (_, _) => URLUtilities.OpenURLInBrowser(feedItem.Link);

                    truncatedContainer.Children.Add(truncatedLink);
                    truncatedContainer.Children.Add(new TextBlock
                    {
                        Text = Properties.Resources.TruncatedClickSuffix,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                    });

                    itemContainer.Children.Add(truncatedContainer);
                }

                targetContainer.Children.Add(itemContainer);
            }
        }
    }
}
