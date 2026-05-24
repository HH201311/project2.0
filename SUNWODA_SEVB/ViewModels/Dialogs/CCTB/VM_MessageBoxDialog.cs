using CsvHelper.Configuration;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Interactivity;
using HandyControl.Properties.Langs;
using HandyControl.Tools;
using HandyControl.Tools.Extension;
using Org.BouncyCastle.Utilities.Encoders;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB.UserOperation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;
using Xceed.Wpf.Toolkit.Core.Converters;
using static System.Resources.ResXFileRef;

namespace SUNWODA_SEVB.ViewModels.Dialogs.CCTB
{
    public class VM_CCTBMessageBoxDialog : DialogViewModelBase, IDialogResultable<MessageBoxResult>
    {
        private bool _showImage;
        private Geometry? _image;
        private Brush? _imageBrush;
        private bool _showTitle;
        private string? _title;
        private string? _message;
        private MessageBoxButtonModel _okButtonModel = new();
        private MessageBoxButtonModel _cancleButtonModel = new();
        private MessageBoxButtonModel _yesButtonModel = new();
        private MessageBoxButtonModel _noButtonModel = new();
        private MessageBoxResult _result;

        /// <summary>
        /// 是否显示图标
        /// </summary>
        public bool ShowImage { get => _showImage; set => SetProperty(ref _showImage, value); }

        /// <summary>
        /// 图标
        /// </summary>
        public Geometry? Image { get => _image; set => SetProperty(ref _image, value); }

        /// <summary>
        /// 图标颜色
        /// </summary>
        public Brush? ImageBrush { get => _imageBrush; set => SetProperty(ref _imageBrush, value); }

        /// <summary>
        /// 是否显示标题
        /// </summary>
        public bool ShowTitle { get => _showTitle; set => SetProperty(ref _showTitle, value); }

        /// <summary>
        /// 标题
        /// </summary>
        public string? Title { get => _title; set => SetProperty(ref _title, value); }

        /// <summary>
        /// 消息
        /// </summary>
        public string? Message { get => _message; set => SetProperty(ref _message, value); }

        /// <summary>
        /// OK按钮
        /// </summary>
        public MessageBoxButtonModel OKButtonModel { get => _okButtonModel; set => SetProperty(ref _okButtonModel, value); }

        /// <summary>
        /// Cancle按钮
        /// </summary>
        public MessageBoxButtonModel CancleButtonModel { get => _cancleButtonModel; set => SetProperty(ref _cancleButtonModel, value); }

        /// <summary>
        /// Yes按钮
        /// </summary>
        public MessageBoxButtonModel YesButtonModel { get => _yesButtonModel; set => SetProperty(ref _yesButtonModel, value); }

        /// <summary>
        /// No按钮
        /// </summary>
        public MessageBoxButtonModel NoButtonModel { get => _noButtonModel; set => SetProperty(ref _noButtonModel, value); }

        /// <summary>
        /// 结果
        /// </summary>
        public MessageBoxResult Result { get => _result; set => SetProperty(ref _result, value); }

        public Action? CloseAction { get; set; }

        /// <summary>
        /// None
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(() => { Result = MessageBoxResult.None; CloseAction?.Invoke(); });

        #region 静态方法
        /// <summary>
        /// 询问
        /// </summary>
        /// <param name="messageBoxText"></param>
        /// <param name="caption"></param>
        /// <returns></returns>
        public static async Task<MessageBoxResult> AskAsync(string messageBoxText, string? caption = null)
        {
            var messageBox = CreateMessageBox(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK);
            SetButtonStatus(messageBox, MessageBoxButton.OK, MessageBoxResult.OK);
            SetImage(messageBox, MessageBoxImage.Question);

            var result = await Dialog.Show(messageBox.UIElement).GetResultAsync<MessageBoxResult>();
            return result;
        }

        /// <summary>
        /// 成功
        /// </summary>
        /// <param name="messageBoxText"></param>
        /// <param name="caption"></param>
        /// <returns></returns>
        public static async Task<MessageBoxResult> SuccessAsync(string messageBoxText, string? caption = null)
        {
            var messageBox = CreateMessageBox(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK);
            SetButtonStatus(messageBox, MessageBoxButton.OK, MessageBoxResult.OK);
            messageBox.ShowImage = true;
            messageBox.Image = ResourceHelper.GetResource<Geometry>("SuccessGeometry");
            messageBox.ImageBrush = ResourceHelper.GetResource<Brush>("SuccessBrush");

            var result = await Dialog.Show(messageBox.UIElement).GetResultAsync<MessageBoxResult>();
            return result;
        }

        /// <summary>
        /// 消息
        /// </summary>
        /// <param name="messageBoxText"></param>
        /// <param name="caption"></param>
        /// <returns></returns>
        public static async Task<MessageBoxResult> InfoAsync(string messageBoxText, string? caption = null)
        {
            var messageBox = CreateMessageBox(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.Asterisk, MessageBoxResult.OK);
            SetButtonStatus(messageBox, MessageBoxButton.OK, MessageBoxResult.OK);
            SetImage(messageBox, MessageBoxImage.Asterisk);

            var result = await Dialog.Show(messageBox.UIElement).GetResultAsync<MessageBoxResult>();
            return result;
        }

        /// <summary>
        /// 警告
        /// </summary>
        /// <param name="messageBoxText"></param>
        /// <param name="caption"></param>
        /// <returns></returns>
        public static async Task<MessageBoxResult> WarningAsync(string messageBoxText, string? caption = null)
        {
            var messageBox = CreateMessageBox(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.Exclamation, MessageBoxResult.OK);
            SetButtonStatus(messageBox, MessageBoxButton.OK, MessageBoxResult.OK);
            SetImage(messageBox, MessageBoxImage.Exclamation);

            var result = await Dialog.Show(messageBox.UIElement).GetResultAsync<MessageBoxResult>();
            return result;
        }

        /// <summary>
        /// 错误
        /// </summary>
        /// <param name="messageBoxText"></param>
        /// <param name="caption"></param>
        /// <returns></returns>
        public static async Task<MessageBoxResult> ErrorAsync(string messageBoxText, string? caption = null)
        {
            var messageBox = CreateMessageBox(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.Hand, MessageBoxResult.OK);
            SetButtonStatus(messageBox, MessageBoxButton.OK, MessageBoxResult.OK);
            SetImage(messageBox, MessageBoxImage.Hand);

            var result = await Dialog.Show(messageBox.UIElement).GetResultAsync<MessageBoxResult>();
            return result;
        }

        /// <summary>
        /// 严重
        /// </summary>
        /// <param name="messageBoxText"></param>
        /// <param name="caption"></param>
        /// <returns></returns>
        public static async Task<MessageBoxResult> FatalAsync(string messageBoxText, string? caption = null)
        {
            var messageBox = CreateMessageBox(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK);
            SetButtonStatus(messageBox, MessageBoxButton.OK, MessageBoxResult.OK);
            messageBox.ShowImage = true;
            messageBox.Image = ResourceHelper.GetResource<Geometry>("FatalGeometry");
            messageBox.ImageBrush = ResourceHelper.GetResource<Brush>("PrimaryTextBrush");

            var result = await Dialog.Show(messageBox.UIElement).GetResultAsync<MessageBoxResult>();
            return result;
        }

        /// <summary>
        /// 自定义展示
        /// </summary>
        /// <param name="messageBoxText"></param>
        /// <param name="caption"></param>
        /// <param name="button"></param>
        /// <param name="icon"></param>
        /// <param name="defaultResult"></param>
        /// <returns></returns>
        public static async Task<MessageBoxResult> ShowAsync(string messageBoxText, string? caption = null, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            var messageBox = CreateMessageBox(messageBoxText, caption, button, icon, defaultResult);
            SetButtonStatus(messageBox, button, defaultResult);
            SetImage(messageBox, icon);

            var result = await Dialog.Show(messageBox.UIElement).GetResultAsync<MessageBoxResult>();
            return result;
        }

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="messageBoxText"></param>
        /// <param name="caption"></param>
        /// <param name="button"></param>
        /// <param name="icon"></param>
        /// <param name="defaultResult"></param>
        /// <returns></returns>
        /// <exception cref="InvalidEnumArgumentException"></exception>
        private static VM_CCTBMessageBoxDialog CreateMessageBox(string messageBoxText, string? caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            if (!IsValidMessageBoxButton(button))
            {
                throw new InvalidEnumArgumentException("button", (int)button, typeof(MessageBoxButton));
            }
            if (!IsValidMessageBoxImage(icon))
            {
                throw new InvalidEnumArgumentException("icon", (int)icon, typeof(MessageBoxImage));
            }
            if (!IsValidMessageBoxResult(defaultResult))
            {
                throw new InvalidEnumArgumentException("defaultResult", (int)defaultResult, typeof(MessageBoxResult));
            }

            var messageBox = new VM_CCTBMessageBoxDialog();
            messageBox.Message = messageBoxText;
            messageBox.ShowTitle = true;
            messageBox.Title = (caption ?? string.Empty);
            messageBox.Result = defaultResult;
            return messageBox;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageBox"></param>
        /// <param name="messageBoxButton"></param>
        /// <param name="defaultResult"></param>
        private static void SetButtonStatus(VM_CCTBMessageBoxDialog messageBox, MessageBoxButton messageBoxButton, MessageBoxResult defaultResult)
        {
            if (messageBoxButton == MessageBoxButton.OK || messageBoxButton == MessageBoxButton.OKCancel)
            {
                messageBox.OKButtonModel.Visibility = Visibility.Visible;
                messageBox.OKButtonModel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3DCC84"));
                messageBox.OKButtonModel.Content = Lang.Confirm;
                messageBox.OKButtonModel.Command = new RelayCommand(() => { messageBox.Result = MessageBoxResult.OK; messageBox.CloseAction?.Invoke(); });
            }

            if (messageBoxButton == MessageBoxButton.YesNo || messageBoxButton == MessageBoxButton.YesNoCancel)
            {
                messageBox.YesButtonModel.Visibility = Visibility.Visible;
                messageBox.YesButtonModel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3DCC84"));
                messageBox.YesButtonModel.Content = Lang.Yes;
                messageBox.YesButtonModel.Command = new RelayCommand(() => { messageBox.Result = MessageBoxResult.Yes; messageBox.CloseAction?.Invoke(); });

                messageBox.NoButtonModel.Visibility = Visibility.Visible;
                messageBox.NoButtonModel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC5252"));
                messageBox.NoButtonModel.Content = Lang.No;
                messageBox.NoButtonModel.Command = new RelayCommand(() => { messageBox.Result = MessageBoxResult.No; messageBox.CloseAction?.Invoke(); });
            }

            if (messageBoxButton == MessageBoxButton.OKCancel || messageBoxButton == MessageBoxButton.YesNoCancel)
            {
                messageBox.CancleButtonModel.Visibility = Visibility.Visible;
                messageBox.CancleButtonModel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("Transparent"));
                messageBox.CancleButtonModel.Content = Lang.Cancel;
                messageBox.CancleButtonModel.Command = new RelayCommand(() => { messageBox.Result = MessageBoxResult.Cancel; messageBox.CloseAction?.Invoke(); });
            }
        }

        /// <summary>
        /// 设置图标
        /// </summary>
        /// <param name="messageBox"></param>
        /// <param name="messageBoxImage"></param>
        private static void SetImage(VM_CCTBMessageBoxDialog messageBox, MessageBoxImage messageBoxImage)
        {
            string text = string.Empty;
            string key = string.Empty;
            switch (messageBoxImage)
            {
                case MessageBoxImage.Hand:
                    text = "ErrorGeometry";
                    key = "DangerBrush";
                    break;
                case MessageBoxImage.Question:
                    text = "AskGeometry";
                    key = "AccentBrush";
                    break;
                case MessageBoxImage.Exclamation:
                    text = "WarningGeometry";
                    key = "WarningBrush";
                    break;
                case MessageBoxImage.Asterisk:
                    text = "InfoGeometry";
                    key = "InfoBrush";
                    break;
            }
            if (!string.IsNullOrEmpty(text))
            {
                messageBox.ShowImage = true;
                messageBox.Image = ResourceHelper.GetResource<Geometry>(text);
                messageBox.ImageBrush = ResourceHelper.GetResource<Brush>(key);
            }
        }

        /// <summary>
        /// 校验按钮类型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool IsValidMessageBoxButton(MessageBoxButton value)
        {
            if (value != 0 && value != MessageBoxButton.OKCancel && value != MessageBoxButton.YesNo)
            {
                return value == MessageBoxButton.YesNoCancel;
            }
            return true;
        }

        /// <summary>
        /// 校验图标类型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool IsValidMessageBoxImage(MessageBoxImage value)
        {
            if (value != MessageBoxImage.Asterisk && value != MessageBoxImage.Hand && value != MessageBoxImage.Exclamation && value != MessageBoxImage.Hand && value != MessageBoxImage.Asterisk && value != 0 && value != MessageBoxImage.Question && value != MessageBoxImage.Hand)
            {
                return value == MessageBoxImage.Exclamation;
            }
            return true;
        }

        /// <summary>
        /// 校验结果类型
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool IsValidMessageBoxResult(MessageBoxResult value)
        {
            if (value != MessageBoxResult.Cancel && value != MessageBoxResult.No && value != 0 && value != MessageBoxResult.OK)
            {
                return value == MessageBoxResult.Yes;
            }
            return true;
        }
        #endregion
    }
}
