using SmartBear.Collaborator.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using SmartBear.Collaborator.API;
using SmartBear.Collaborator.API.Json;
using SmartBear.Collaborator.API.Log;

namespace SmartBear.Collaborator.Script
{
    class Script
    {
        static readonly string[] _checkingParticipantRoles = { "Author", "Review Lead or RL/Author", "Required Reviewer" };
        static readonly string[] _notifiedParticipantRoles = { "Author", "Review Lead or RL/Author" };
        static readonly string[] _nonCheckingParticipantRoles = { "Invitee Reviewer" };
        static List<SystemAdmin.CustomFieldSettingsTarget> _checkingParticipantCustomFields;

        const double AcceptableLevelOfExternalDefects = 0.1d;  //10%
        const int HundredPercent = 100;
        const double AdmissibleCountOfAllDefects = 2;
        const int CountOfParsingArguments = 11;
        const double AdmissibleCountOfExternalDefects = 1;
        const char ArgumentSplitter = '|';

        const string CompletedStateName = "Completed";
        const string ExternalStateName = "External";
        const string CheckingBugCustomFieldName = "Correction Implemented by Author (Optional)";
        const string CheckingBugCustomFieldValue = "Implemented";
        const string CheckingTemplateName = "Standart roles";
        const string ExternalBugTypeName = "External";

        //Only for message. Sets as parameter of trigger.
        const string ReviewCustomFieldName = "Work Product Tag(s)*";

        const string EmailLineStart = "%0A - ";

        /*???*/
        const string EmailMessageTitleIntro = "Collab script found some problems with the review.";

        /*???*/
        const string EmailBodyIntro = "The list of problems with the review is given below:";

        const string CustomPathToLogFolder = @"C:\Users\Sergei.Yakovlev\Desktop\CC9824";

        const string EmailMessageTitleFormat = "Review #{0}: {1}";

        /*???*/
        const string EmailMessageFormat = "E-mail messages has been send to {0} adresses";

        /*???*/
        const string UserCustomFieldProblemFormat = "User %22{0}%22 with role %22{1}%22 has problems with custom fields. Field %22{2}%22 has different value then %22{3}%22";

        /*???*/
        const string BugCustomFieldProblemFormat = "Bug with id {0} has private field %22{1}%22 with value differ then %22{2}%22";

        /*???*/
        const string LogPhaseErrorFormat = "Script: Error. Review phase is not ";

        /*???*/
        const string LogRulesErrorFormat = "Script: Error. Current User {0} has no admin rules";

        /*???*/
        const string LogReopenErrorFormat = "Error. Cannot reopen review with id {0}.";

        /*???*/
        const string UsedTemplateProblemFormat = "Used Template is not %22{0}%22";

        /*???*/
        const string ReviewCustomFieldProblemFormat = "Custom field %22{0}%22 value is empty";

        /*???*/
        const string ExternalDefectsProblemFormat = "External defects level is more than {0}%";

        /*???*/
        const string LogJsonError = "Script: Error while working with JSON";

        /*???*/
        const string LogParsingError = "Script: Error while parsing arguments";

        #region Indexes of input argument's array
        const int CurrentReviewIdIdx = 0;
        const int CurrentReviewTemplateNameIdx = 1;
        const int CurrentReviewPhaseIdx = 2;
        const int ServerURLIdx = 3;
        const int UserNameIdx = 4;
        const int UserPasswordIdx = 5;
        const int UseProxyIdx = 6;
        const int ProxyHostIdx = 7;
        const int ProxyPortIdx = 8;
        const int ProxyLoginIdx = 9;
        const int ProxyPasswordIdx = 10;
        #endregion

        List<IMessage> _messages;
        Review.ReviewSummary _reviewSummary;
        List<User.UserInfo> _usersInfo;
        ConnectionSettings _serverConfig;
        string _currentReviewPhase;
        int _currentReviewId;
        string _currentReviewTemplateName;
        ILogger _logger;
        ILoggerFactory _loggerFactory;

        public Script()
        {
            Initialize();
        }

        #region Initialize Script
        private void Initialize()
        {
            _messages = new List<IMessage>();
            _serverConfig = new ConnectionSettings();

            //It's possible to use your own created logger. As example - you can implement CustomLogger
            //Change BaseLoggerFactory to CustomLoggerFactory here.
            //Custom logger will be used in script and API.
            _loggerFactory = new BaseLoggerFactory();
            _logger = _loggerFactory.GetLogger(this.GetType());
            _logger.CustomPathToLogFolder = CustomPathToLogFolder;

            ConfigureCheckingCustomFields();
        }

        private void ConfigureCheckingCustomFields()
        {
            _checkingParticipantCustomFields = new List<SystemAdmin.CustomFieldSettingsTarget>();
            _checkingParticipantCustomFields.Add(CreateCheckingParticipantCustomField("Reviewer Status*", "Review Acceptable"));
        }
        #endregion

        private void StopServerConnection(IServer server)
        {
            server?.Dispose();
        }

        /*???*/
        /// <summary>
        /// Function that checking valid of asking review custom field.
        /// </summary>
        /// <param name="reviewCustomFieldName">Checking custom field name</param>
        /// <param name="customFieldValue">List of review custom fields</param>
        /// <returns>True if value is valid, false if not</returns>
        private bool CheckReviewCustomFieldRegardingAnError(List<SystemAdmin.CustomFieldSettingsTarget> customFieldValue, string reviewCustomFieldName)
        {
            const int FirstLineIndex = 0;

            if (customFieldValue == null)
            {
                return false;
            }

            SystemAdmin.CustomFieldSettingsTarget checkingCustomField = customFieldValue.Find(p => CompareStrings(p.customFieldTitle, reviewCustomFieldName));
            if (checkingCustomField == null)
            {
                return false;
            }

            return IsStringEmptyOrWhiteSpace(checkingCustomField.customFieldValue[FirstLineIndex]);
        }

        private bool IsCurrentReviewPhaseCompleted(string currentReviewPhase)
        {
            return CompareStrings(currentReviewPhase, CompletedStateName);
        }

        private SystemAdmin.CustomFieldSettingsTarget CreateCheckingParticipantCustomField(string title, string value)
        {
            SystemAdmin.CustomFieldSettingsTarget customField = new SystemAdmin.CustomFieldSettingsTarget();
            customField.customFieldTitle = title;
            customField.customFieldValue = new List<string>();
            customField.customFieldValue.Add(value);
            return customField;
        }

        private bool IsCurrentUserIsAdmin(List<User.UserInfo> usersInfo, string userName)
        {
            return usersInfo.Exists(p => ((p.login == userName) && (p.admin)));
        }

        private bool IsStringEmptyOrWhiteSpace(string reviewCustomFieldValue)
        {
            return string.IsNullOrWhiteSpace(reviewCustomFieldValue);
        }

        private bool CheckUsedTemplate(string currentReviewTemplateName, string checkingTemplateName)
        {
            return CompareStrings(_currentReviewTemplateName, CheckingTemplateName);
        }

        /*???*/
        /// <summary>
        /// Function that checking roles for each participant and participant's custom field value.
        /// </summary>
        /// <param name="reviewParticipants">List with information about review participants</param>
        private void CheckParticipantsRolesAndCustomFields(List<Review.ReviewParticipant> reviewParticipants)
        {
            foreach (Review.ReviewParticipant participant in reviewParticipants)
            {
                if (_nonCheckingParticipantRoles.Contains(participant.role.name))
                {
                    continue;
                }

                if (_checkingParticipantRoles.Contains(participant.role.name))
                {
                    CheckCustomFieldValueOfParticipant(participant, _checkingParticipantCustomFields);
                }
            }
        }

        private bool IsCheckingCustomFieldValueValid(SystemAdmin.CustomFieldSettingsTarget p, SystemAdmin.CustomFieldSettingsTarget checkingCustomField)
        {
            return (p.customFieldValue.Contains(checkingCustomField.customFieldValue[0]));
        }

        private void SendEmailMessagesToParticipants(List<IMessage> messages, List<User.UserInfo> reviewParticipantsInfo, List<Review.ReviewParticipant> reviewParticipants)
        {
            if (messages.Count == 0)
            {
                return;
            }

            List<string> emails = new List<string>();

            if (messages.Exists(p => p.GetType() == typeof(ErrorMessage)))
            {
                reviewParticipants.ForEach(reviewParticipant => emails.Add(reviewParticipantsInfo.Find(participantInfo => participantInfo.id.Equals(reviewParticipant.user.id)).email));

                bool isReviewReopened = ReopenReview(_currentReviewId);
                if (!isReviewReopened)
                {
                    _logger.LogError(String.Format(LogReopenErrorFormat, _currentReviewId));
                }

            }
            else
            {
                List<Review.ReviewParticipant> specialReviewParticipants = reviewParticipants.FindAll(p => _notifiedParticipantRoles.Contains(p.role.name));

                /*???*/
                //Review Participant's user has no info about his email in current version of Json Api.
                specialReviewParticipants.ForEach(specialParticipant => emails.Add(reviewParticipantsInfo.Find(participantInfo => participantInfo.id.Equals(specialParticipant.user.id)).email));
            }

            SendEmailMessages(messages, emails);
            _logger.LogInfoMessage(String.Format(EmailMessageFormat, emails.Count));
        }

        private bool ReopenReview(int currentReviewId)
        {
            bool result = false;
            IServer server;

            if (CreateServer(_serverConfig, out server))
            {
                server.ReviewService.ReopenReview(_currentReviewId);
                result = IsReviewReopened(server);
                StopServerConnection(server);
            }

            return result;
        }

        private bool IsReviewReopened(IServer server)
        {
            Review.ReviewInfo reviewInfo = server.ReviewService.GetReviewInfo(_currentReviewId);

            return !CompareStrings(reviewInfo.reviewPhase.ToString(), CompletedStateName);
        }

        private void SendEmailMessages(List<IMessage> messages, List<string> emails)
        {
            emails.RemoveAll(p => IsStringEmptyOrWhiteSpace(p));
            MailToURLBuilder mailToURLBuilder = new MailToURLBuilder(emails);
            mailToURLBuilder.SetSubject(
                String.Format(
                    EmailMessageTitleFormat,
                    _currentReviewId,
                    EmailMessageTitleIntro
                    )
                );
            string newEmailBody = EmailBodyIntro;

            foreach (IMessage message in messages)
            {
                newEmailBody += EmailLineStart + message.MessageText;
            }

            mailToURLBuilder.SetBody(newEmailBody);
            string emailUrl = mailToURLBuilder.ToUrl();
            System.Diagnostics.Process.Start(emailUrl);
        }

        private void CheckCustomFieldValueOfParticipant(Review.ReviewParticipant participant, List<SystemAdmin.CustomFieldSettingsTarget> checkingParticipantCustomFields)
        {
            int checkingCustomFieldPosition;

            foreach (SystemAdmin.CustomFieldSettingsTarget checkingCustomField in checkingParticipantCustomFields)
            {
                if (!IsCheckingCustomFieldExist(participant.customFieldValue, checkingCustomField.customFieldTitle, out checkingCustomFieldPosition))
                {
                    continue;
                }

                if (!IsCheckingCustomFieldValueValid(participant.customFieldValue[checkingCustomFieldPosition], checkingCustomField))
                {
                    StoreAsError(String.Format(UserCustomFieldProblemFormat,
                        participant.user.fullName, participant.role.name, checkingCustomField.customFieldTitle, checkingCustomField.customFieldValue[0]));
                }
            }
        }

        /*???*/
        /// <summary>
        /// Function that checks custom field's value of review's non-external bugs.
        /// </summary>
        /// <param name="customFieldName">Name of cheking custom field</param>
        /// <param name="customFieldValue">Value of cheking custom field</param>
        /// <param name="defectLogEntrys">List with info about every bug</param>
        private void CheckCustomFieldValueOfNonExternalBugs(string customFieldName, string customFieldValue, List<Review.DefectLogEntry> defectLogEntrys)
        {
            foreach (Review.DefectLogEntry defectLogEntry in defectLogEntrys.Where(p => !CompareStrings(p.defectSummary.state, ExternalBugTypeName)))
            {
                int positionOfCustomFieldValue;
                List<SystemAdmin.CustomFieldSettingsTarget> customFieldList = defectLogEntry.defectSummary.customFieldValue;

                if (!IsCheckingCustomFieldExist(customFieldList, customFieldName, out positionOfCustomFieldValue))
                {
                    continue;
                }

                if (!customFieldList[positionOfCustomFieldValue].customFieldValue.Contains(customFieldValue))
                {
                    StoreAsWarning(String.Format(BugCustomFieldProblemFormat, defectLogEntry.defectSummary.defectId,
                        CheckingBugCustomFieldName, CheckingBugCustomFieldValue));
                }
            }
        }

        private bool IsCheckingCustomFieldExist(List<SystemAdmin.CustomFieldSettingsTarget> customFieldValue,
            string customFieldName, out int checkingCustomFieldPosition)
        {
            checkingCustomFieldPosition = customFieldValue.FindIndex(p => CompareStrings(p.customFieldTitle, customFieldName));
            return checkingCustomFieldPosition != -1;
        }

        private int GetCountOfExternalDefects(List<Review.DefectLogEntry> defectLogEntrys)
        {
            return defectLogEntrys.Count(p => CompareStrings(p.defectSummary.state, ExternalStateName));
        }

        private double GetLevelOfExternalDefects(double countOfAllDefects, double countOfExternalDefects)
        {
            if (countOfAllDefects != 0)
            {
                return (countOfExternalDefects / countOfAllDefects);
            }
            else
            {
                return 0;
            }
        }

        /*???*/
        /// <summary>
        /// Function that checking level of external defects.
        /// </summary>
        /// <param name="defectLogEntrys">List with info about all bugs</param>
        /// <returns>True if level of external defects is less then acceptable level or if there are only 2 total defects and 1 is external, false if not</returns>
        private bool CheckLevelOfExternalDefects(List<Review.DefectLogEntry> defectLogEntrys)
        {
            int countOfExternalDefects = GetCountOfExternalDefects(defectLogEntrys);
            int countOfAllDefects = defectLogEntrys.Count;
            double levelOfExternalDefects = GetLevelOfExternalDefects(countOfAllDefects, countOfExternalDefects);
            return (levelOfExternalDefects < AcceptableLevelOfExternalDefects) || IsAdditionalConditions(countOfAllDefects, countOfExternalDefects);
        }

        /*???*/
        /// <summary>
        /// Function that checking additional conditions. Condition - review has 2 bugs and 1 of them is external
        /// </summary>
        /// <param name="countOfAllDefects">Count of all defects in review</param>
        /// <param name="countOfExternalDefects">Count of all external defects in review</param>
        /// <returns></returns>
        private bool IsAdditionalConditions(int countOfAllDefects, int countOfExternalDefects)
        {
            return ((countOfAllDefects == AdmissibleCountOfAllDefects) && (countOfExternalDefects == AdmissibleCountOfExternalDefects));
        }

        private void StoreAsWarning(string warningMessage)
        {
            _messages.Add(new WarningMessage(warningMessage));
        }

        private void StoreAsError(string errorMessage)
        {
            _messages.Add(new ErrorMessage(errorMessage));
        }

        private bool CompareStrings(string A, string B)
        {
            return String.Compare(A, B, true) == 0;
        }

        /*???*/
        /// <summary>
        /// Function that try get additional input arguments from Json Api.
        /// </summary>
        /// <returns>True if connection was successful, false if not</returns>
        private bool GetJsonData()
        {
            IServer server;

            if (CreateServer(_serverConfig, out server))
            {
                _reviewSummary = server.ReviewService.GetReviewSummary(_currentReviewId);
                _usersInfo = server.UserService.GetUserList();
                StopServerConnection(server);
                return true;
            }

            _logger.LogError(LogJsonError);
            return false;
        }

        /*???*/
        /// <summary>
        /// Creating server instance and connecting it to collab server.
        /// </summary>
        /// <param name="serverConfig">Info about connection settings</param>
        /// <param name="server">Connected instance of IServer</param>
        /// <returns></returns>
        private bool CreateServer(ConnectionSettings serverConfig, out IServer server)
        {
            server = new Server();
            server.LoggerFactory = _loggerFactory;
            return server.Connect(_serverConfig);
        }

        /*???*/
        /// <summary>
        /// Function that try to parse input arguments.
        /// </summary>
        /// <param name="args">Arguments getting as trigger parameters</param>
        /// <returns>True if parse was successful, false if not</returns>
        private bool ParseArgs(string[] args)
        {
            string[] sublines;
            bool useProxy;
            int proxyPort;

            if (args == null)
            {
                return false;
            }

            if (args.Length == 0)
            {
                return false;
            }

            sublines = args[0].Split(ArgumentSplitter);
            if (sublines.Length != CountOfParsingArguments)
            {
                return false;
            }

            if (!Int32.TryParse(sublines[CurrentReviewIdIdx], out _currentReviewId))
            {
                return false;
            }

            _currentReviewTemplateName = sublines[CurrentReviewTemplateNameIdx];
            _currentReviewPhase = sublines[CurrentReviewPhaseIdx];
            _serverConfig.ServerURL = sublines[ServerURLIdx];
            _serverConfig.UserName = sublines[UserNameIdx];
            _serverConfig.UserPassword = sublines[UserPasswordIdx];

            if (!Boolean.TryParse(sublines[UseProxyIdx], out useProxy))
            {
                return false;
            }

            _serverConfig.UseProxy = useProxy;
            _serverConfig.ProxyHost = sublines[ProxyHostIdx];

            if (!Int32.TryParse(sublines[ProxyPortIdx], out proxyPort))
            {
                return false;
            }

            _serverConfig.ProxyPort = proxyPort;
            _serverConfig.ProxyLogin = sublines[ProxyLoginIdx];
            _serverConfig.ProxyPassword = sublines[ProxyPasswordIdx];
            return true;
        }

        #region Execute Script
        public void Execute(string[] args)
        {
            if (!ParseArgs(args))
            {
                _logger.LogError(LogParsingError);
                return;
            }

            if (!GetJsonData())
            {
                _logger.LogError(LogJsonError);
                return;
            }

            if (!IsCurrentReviewPhaseCompleted(_currentReviewPhase))
            {
                _logger.LogError(LogPhaseErrorFormat + _currentReviewPhase);
                return;
            }

            if (!IsCurrentUserIsAdmin(_usersInfo, _serverConfig.UserName))
            {
                _logger.LogError(String.Format(LogRulesErrorFormat, _serverConfig.UserName));
                return;
            }

            if (!CheckUsedTemplate(_currentReviewTemplateName, CheckingTemplateName))
            {
                StoreAsWarning(String.Format(UsedTemplateProblemFormat, CheckingTemplateName));
            }

            if (CheckReviewCustomFieldRegardingAnError(_reviewSummary.generalInfo.customFieldValue, ReviewCustomFieldName))
            {
                StoreAsError(String.Format(ReviewCustomFieldProblemFormat, ReviewCustomFieldName));
            }

            if (!CheckLevelOfExternalDefects(_reviewSummary.defectLogEntrys))
            {
                StoreAsWarning(String.Format(ExternalDefectsProblemFormat, AcceptableLevelOfExternalDefects * HundredPercent));
            }

            CheckCustomFieldValueOfNonExternalBugs(CheckingBugCustomFieldName, CheckingBugCustomFieldValue, _reviewSummary.defectLogEntrys);
            CheckParticipantsRolesAndCustomFields(_reviewSummary.reviewParticipants);
            SendEmailMessagesToParticipants(_messages, _usersInfo, _reviewSummary.reviewParticipants);
        }
        #endregion
    }
}
