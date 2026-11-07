; *** Inno Setup version 6.5.0+ Serbian (Cyrillic) messages ***
; Derived from the corrected Croatian, 2026-08-24 -- DRAFT, needs a native check
; Based on translation by Elvis Gambiraža (el.gambo@gmail.com)
; Based on translation by Krunoslav Kanjuh (krunoslav.kanjuh@zg.t-com.hr)
;
; To download user-contributed translations of this file, go to:
;   https://www.jrsoftware.org/files/istrans/
;
; Note: When translating this text, do not add periods (.) to the end of
; messages that didn't have them already, because on those messages Inno
; Setup adds the periods automatically (appending a period would result in
; two periods being displayed).

[LangOptions]
; The following three entries are very important. Be sure to read and 
; understand the '[LangOptions] section' topic in the help file.
LanguageName=<0421><0440><043F><0441><043A><0438>
LanguageID=$0C1A
LanguageCodePage=1251
; If the language you are translating to requires special font faces or
; sizes, uncomment any of the following entries and change them accordingly.
;DialogFontName=MS Shell Dlg
;DialogFontSize=8
;WelcomeFontName=Arial
;WelcomeFontSize=12
;TitleFontName=Arial
;TitleFontSize=29
;CopyrightFontName=Arial
;CopyrightFontSize=8

[Messages]

; *** Application titles
SetupAppTitle=Постављање апликације
SetupWindowTitle=Постављање – %1
UninstallAppTitle=Уклањање
UninstallAppFullTitle=Уклањање апликације %1

; *** Misc. common
InformationTitle=Обавештење
ConfirmTitle=Потврда
ErrorTitle=Грешка

; *** SetupLdr messages
SetupLdrStartupMessage=Овиме ће се инсталирати %1. Желиш ли наставити?
LdrCannotCreateTemp=Није могуће створити привремену датотеку. Инсталација је прекинута
LdrCannotExecTemp=Није могуће покренути датотеку у привременој фасцикли. Инсталација је прекинута
HelpTextNote=

; *** Startup error messages
LastErrorMessage=%1.%n%nнГрешка %2: %3
SetupFileMissing=Датотека %1 се не налази у фасцикли инсталације. Исправи проблем или набави нову копију програма.
SetupFileCorrupt=Датотеке инсталације су оштећене. Набави нову копију програма.
SetupFileCorruptOrWrongVer=Датотеке инсталације су оштећене или нису компатибилне с овом верзијом инсталације. Исправи проблем или набави нову копију програма.
InvalidParameter=Неисправан параметар је пренет у наредбеном ретку:%n%n%1
SetupAlreadyRunning=Инсталација је већ покренута.
WindowsVersionNotSupported=Програм не подржава Windows верзију коју користиш.
WindowsServicePackRequired=Програм захтева %1 сервисни пакет %2 или новији.
NotOnThisPlatform=Програм неће радити на %1.
OnlyOnThisPlatform=Програм се мора покренути на %1.
OnlyOnTheseArchitectures=Програм се може инсталирати на Windows верзијама за следеће процесорске архитектуре:%n%n%1
WinVersionTooLowError=Програм захтева %1 верзију %2 или новију.
WinVersionTooHighError=Програм се не може инсталирати на %1 верзији %2 или новијој.
AdminPrivilegesRequired=За инсталирање програма мораш бити пријављен/а као администратор.
PowerUserPrivilegesRequired=За инсталирање програма мораш бити пријављен/а као администратор или као члан групе напредних корисника.
SetupAppRunningError=Инсталација је открила да је %1 тренутачно покренут.%n%nЗатвори програм и потом притисни "Dalje" за наставак или "Odustani" за прекид.
UninstallAppRunningError=Деинсталација је открила да је %1 тренутачно покренут.%n%nЗатвори програм и потом притисни "Dalje" за наставак или "Odustani" за прекид.

; *** Startup questions
PrivilegesRequiredOverrideTitle=Одабери начин инсталирања
PrivilegesRequiredOverrideInstruction=Одабери начин постављања
PrivilegesRequiredOverrideText1=%1 може се поставити за вас или за све кориснике (потребна су администраторска права).
PrivilegesRequiredOverrideText2=%1 може се поставити за вас или за све кориснике (потребна су администраторска права).
PrivilegesRequiredOverrideAllUsers=Пост&ави за све кориснике
PrivilegesRequiredOverrideAllUsersRecommended=Пост&ави за све кориснике (препоручено)
PrivilegesRequiredOverrideCurrentUser=Постави само за &мене
PrivilegesRequiredOverrideCurrentUserRecommended=Постави само за &мене (препоручено)

; *** Misc. errors
ErrorCreatingDir=Инсталација није могла створити фасциклу "%1"
ErrorTooManyFilesInDir=Датотеку није могуће створити у фасцикли "%1", јер фасцикла садржи превише датотека

; *** Setup common messages
ExitSetupTitle=Заустави постављање
ExitSetupMessage=Постављање није завршено. Ако сада прекинете апликација се неће поставити.%n%nПостављање можете завршити касније.%n%nПрекинути сада?
AboutSetupMenuItem=&О постављању...
AboutSetupTitle=О постављању
AboutSetupMessage=%1 Иначица %2%n%3%n%n%1 Web страница:%n%4
AboutSetupNote=
TranslatorNote=Преводитељи:%n%nКрунослав Кањух%n%nЕлвис Гамбиража%n%nМило Ивир%n%nГордан Радић

; *** Buttons
ButtonBack=Пре&тходно
ButtonNext=Сле&деће
ButtonInstall=Постав&и
ButtonOK=У реду
ButtonCancel=Откажи
ButtonYes=&Да
ButtonYesToAll=Д&а за све
ButtonNo=&Не
ButtonNoToAll=Н&е за све
ButtonFinish=&Заврши
ButtonBrowse=&Прегледај...
ButtonWizardBrowse=П&регледај...
ButtonNewFolder=&Створи нову фасциклу

; *** "Select Language" dialog messages
SelectLanguageTitle=Изаберите језик постављања
SelectLanguageLabel=Изаберите језик за поступак постављања

; *** Common wizard text
ClickNext=Притисните Следеће за наставак или откажи за прекид постављања
BeveledLabel=
BrowseDialogTitle=Изаберите фасциклу
BrowseDialogLabel=Изаберите фасциклу из пописа и притисните У реду.
NewFolderName=Нова фасцикла

; *** "Welcome" wizard page
WelcomeLabel1=Чаробњак за постављање апликације[name]
WelcomeLabel2=Ускоро ћете почети с постављањем апликације [name/ver].%n%nПрепоручујемо да пре следећег корака затворите све активне апликације.

; *** "Password" wizard page
WizardPassword=Лозинка
PasswordLabel1=Инсталација је заштићена лозинком.
PasswordLabel3=Упиши лозинку и притисни "Dalje". Лозинке су осјетљиве на мала и велика слова.
PasswordEditLabel=&Лозинка:
IncorrectPassword=Уписана је погрешна лозинка. Покушај поново.

; *** "License Agreement" wizard page
WizardLicense=Лиценцни уговор
LicenseLabel=Молимо прочитајте следеће податке пре наставка.
LicenseLabel3=Молимо прочитајте уговор. За наставак морате прихватити увјете уговора.
LicenseAccepted=&Прихваћам
LicenseNotAccepted=&Не прихваћам

; *** "Information" wizard pages
WizardInfoBefore=Обавештењеи
InfoBeforeLabel=Молимо прочитајте следеће обавештења пре наставка
InfoBeforeClickLabel=За наставак притисните Следеће
WizardInfoAfter=Обавештењеи
InfoAfterLabel=Молимо прочитајте следеће обавештења пре наставка
InfoAfterClickLabel=За наставак притисните Следеће

; *** "User Information" wizard page
WizardUserInfo=Кориснички подаци
UserInfoDesc=Упиши своје податке.
UserInfoName=&Име корисника:
UserInfoOrg=&Организација:
UserInfoSerial=&Серијски број:
UserInfoNameRequired=Име је обавезно поље.

; *** "Select Destination Location" wizard page
WizardSelectDir=Изаберите фасциклу за постављање
SelectDirDesc=Гдје желите поставити [name]?
SelectDirLabel3=[name] ће се поставити у ову фасциклу:
SelectDirBrowseLabel=За наставак постављања притисните Следеће. За одабир друге фасцикле притисните Прегледај.
DiskSpaceGBLabel=Потребно је барем [gb] GB слободног простора на диску.
DiskSpaceMBLabel=Потребно је барем [mb] MB слободног простора на диску.
CannotInstallToNetworkDrive=Апликација се не може поставити на мрежни погон
CannotInstallToUNCPath=Апликација се не може поставити на UNC путању
InvalidPath=Морате уписати пуну путању са словом погона, нпр:%n%nЦ:\APP%n%n или UNC путању у облику: %n%n\\послужитељ\схаре
InvalidDrive=Изабрани погон не постоји. Изаберите други.
DiskSpaceWarningTitle=Недовољно простора на изабраном погону
DiskSpaceWarning=За постављање је потребно барем %1 KB слободног простора, но изабрани погон има само %2 KB.%n%n Свеједно наставити?
DirNameTooLong=Путања или име фасцикле су предугачки
InvalidDirName=Име фасцикле је неисправно
BadDirName32=Име фасцикле не сме садржавати следеће знакове:%n%n%1
DirExistsTitle=Фасцикла већ постоји
DirExists=Фасцикла:%n%n%1%n%nвећ постоји. Свеједно поставити?
DirDoesntExistTitle=Фасцикла не постоји
DirDoesntExist=Фасцикла:%n%n%1%n%nне постоји. Желите ли је створити?

; *** "Select Components" wizard page
WizardSelectComponents=Одабери компоненте
SelectComponentsDesc=Које компоненте желиш инсталирати?
SelectComponentsLabel2=Одабери компоненте које желиш инсталирати, искључи компоненте које не желиш инсталирати. За наставак инсталације притисни "Dalje".
FullInstallation=Комплетна инсталација
; if possible don't translate 'Compact' as 'Minimal' (I mean 'Minimal' in your language)
CompactInstallation=Компактна инсталација
CustomInstallation=Прилагођена инсталација
NoUninstallWarningTitle=Постојеће компоненте
NoUninstallWarning=Инсталација је утврдила да на твом рачунару већ постоје следеће компоненте:%n%n%1%n%nИскључивањем тих компонената, оне се неће деинсталирати.%n%nЖелиш ли свеједно наставити?
ComponentSize1=%1 KB
ComponentSize2=%1 MB
ComponentsDiskSpaceGBLabel=Тренутачни одабир захтева барем [gb] GB на диску.
ComponentsDiskSpaceMBLabel=Тренутачни одабир захтева барем [mb] MB на диску.

; *** "Select Additional Tasks" wizard page
WizardSelectTasks=Изаберите следеће поступке
SelectTasksDesc=Које додатне поступке желите направити?
SelectTasksLabel2=Изаберите поступке који ће се извршити приликом постављања апликације [name], а затим притисните Следеће.

; *** "Select Start Menu Folder" wizard page
WizardSelectProgramGroup=Изаберите фасциклу у мениу Start
SelectStartMenuFolderDesc=Гдје желите поставити пречаце апликације?
SelectStartMenuFolderLabel3=Пречаци апликације поставит ће се у ову фасциклу мениа Start
SelectStartMenuFolderBrowseLabel=За наставак притисните Следеће, за одабир друге фасцикле притисните Прегледај
MustEnterGroupName=Име фасцикле је обавезно
GroupNameTooLong=Путања или име фасцикле су предугачки
InvalidGroupName=Име фасцикле није исправно
BadGroupName=Име фасцикле не сме садржавати следеће знакове:%n%n%1
NoProgramGroupCheck2=&Немој створити фасциклу у мениу Start

; *** "Ready to Install" wizard page
WizardReady=Све је спремно за постављање
ReadyLabel1=Све је спремно за постављање апликације[name].
ReadyLabel2a=За постављање притисните Постави. За провјеру опција постављања притисните Натраг
ReadyLabel2b=За постављање притисните Постави
ReadyMemoUserInfo=Кориснички подаци:
ReadyMemoDir=Одредишно место:
ReadyMemoType=Начин постављања:
ReadyMemoComponents=Изабране компоненте:
ReadyMemoGroup=Фасцикла у мениу Start:
ReadyMemoTasks=Додатни задаци:

; *** TDownloadWizardPage wizard page and DownloadTemporaryFile
DownloadingLabel2=Преузимање датотека …
ButtonStopDownload=&Прекини преузимање
StopDownload=Стварно желиш прекинути преузимање?
ErrorDownloadAborted=Преузимање је прекинуто
ErrorDownloadFailed=Неуспјело преузимање: %1 %2
ErrorDownloadSizeFailed=Неуспјело дохваћање величине: %1 %2
ErrorProgress=Неисправан напредак: %1 од %2
ErrorFileSize=Неисправна величина датотеке: очекивано %1, пронађено %2

; *** TExtractionWizardPage wizard page and Extract7ZipArchive
ExtractingLabel=Распакиравање датотека …
ButtonStopExtraction=&Прекини распакиравање
StopExtraction=Стварно желиш прекинути распакиравање?
ErrorExtractionAborted=Распакиравање прекинуто
ErrorExtractionFailed=Распакиравање неуспјело: %1

; *** Archive extraction failure details
ArchiveIncorrectPassword=Лозинка је неисправна
ArchiveIsCorrupted=Архива је покварена
ArchiveUnsupportedFormat=Формат архиве није подржан

; *** "Preparing to Install" wizard page
WizardPreparing=Припрема постављања
PreparingDesc=Припремамо постављање апликације[name].
PreviousInstallNotCompleted=Постављање или уклањање претходне апликације није завршило. Морате поново покренути рачунаро и завршити постављање.%n%n Након поновог покретања рачунара покрените опет постављање апликације [name].
CannotContinue=Постављање не може наставити. Притисните Излаз за прекидање постављања.
ApplicationsFound=Ови програми користе датотеке које постављање мора ажурирати. Предлажемо да затворите наведене програме.
ApplicationsFound2=Ови програми користе датотеке које постављање мора ажурирати. Предлажемо да затворите наведене програме. Кад постављање заврши покушат ћемо их поново покренути.
CloseApplications=&Затвори аутоматски
DontCloseApplications=&Не затварај
ErrorCloseApplications=Нисмо успјели затворити све програме аутоматски. Молимо да покушате затворити програме ручно.
PrepareToInstallNeedsRestart=Потребноје поново покренути рачунаро. Након тога поново покрените постављање апликације [name] како бисмо наставили постављање.%n%nПоново покренути рачунаро?

; *** "Installing" wizard page
WizardInstalling=Постављање
InstallingLabel=Молимо причекајте...

; *** "Setup Completed" wizard page
FinishedHeadingLabel=Завршавање чаробњака за постављање [name]
FinishedLabelNoIcons=Апликација [name] је успешно постављена.
FinishedLabel=Апликација [name] је успешно постављена. Можете је покренути на постављеним пречацима.
ClickFinish=За довршавање поставјања притисните Заврши.
FinishedRestartLabel=За завршетак постављања потребно је поново покренути рачунаро.%n%nНаправити то одмах?
FinishedRestartMessage=За завршетак постављања потребно је поново покренути рачунаро.%n%nНаправити то одмах?
ShowReadmeCheck=Да, желим прочитати README датотеку
YesRadio=&Да, поново покрени
NoRadio=&Не, направит ћу то касније
; used for example as 'Run MyProg.exe'
RunEntryExec=Покрени %1
; used for example as 'View Readme.txt'
RunEntryShellExec=Прикажи %1

; *** "Setup Needs the Next Disk" stuff
ChangeDiskTitle=Инсталација треба следећи диск
SelectDiskLabel2=Уметни диск %1 и притисни "U redu".%n%nАко се датотеке с овог диска налазе на неком другом месту од доље приказаног, упиши исправну стазу или притисни "Odaberi".
PathLabel=&Стаза:
FileNotInDir2=Стаза "%1" не постоји у "%2". Уметни одговарајући диск или одабери једну другу фасциклу.
SelectDirectoryLabel=Одреди место следећег диска.

; *** Installation phase messages
SetupAborted=Постављање није довршено.%n%nИсправите проблем и покушајте поново.
AbortRetryIgnoreSelectAction=Изаберите радњу
AbortRetryIgnoreRetry=&Покушај поново
AbortRetryIgnoreIgnore=&Занемари грешку и настави
AbortRetryIgnoreCancel=Прекини постављање
RetryCancelSelectAction=Одабери радњу
RetryCancelRetry=&Покушај поново
RetryCancelCancel=Откажи

; *** Installation status messages
StatusClosingApplications=Затварање програма …
StatusCreateDirs=Стварање фасцикли …
StatusExtractFiles=Распакирање датотека …
StatusDownloadFiles=Преузимање датотека …
StatusCreateIcons=Стварање пречаца …
StatusCreateIniEntries=Стварање INI уноса …
StatusCreateRegistryEntries=Стварање уноса у регистар …
StatusRegisterFiles=Регистрирање датотека …
StatusSavingUninstall=Спремање података за уклањање...
StatusRunProgram=Завршавање постављања
StatusRestartingApplications=Поново покретање програма …
StatusRollback=Поништавање промена …

; *** Misc. errors
ErrorInternal2=Интерна грешка: %1
ErrorFunctionFailedNoCode=%1 – неуспјело
ErrorFunctionFailed=%1 – неуспјело; код %2
ErrorFunctionFailedWithMessage=%1 – неуспјело; код %2.%n%3
ErrorExecutingProgram=Није могуће извршити датотеку:%n%1

; *** Registry errors
ErrorRegOpenKey=Грешка приликом отварања кључа регистра:%n%1\%2
ErrorRegCreateKey=Грешка приликом стварања кључа регистра:%n%1\%2
ErrorRegWriteKey=Грешка приликом писања у кључ регистра:%n%1\%2

; *** INI errors
ErrorIniEntry=Грешка приликом стварања INI уноса у датотеци "%1".

; *** File copying errors
FileAbortRetryIgnoreSkipNotRecommended=&Прескочи ову датотеку (не препоручује се)
FileAbortRetryIgnoreIgnoreNotRecommended=&Занемари грешку и настави (не препоручује се)
SourceIsCorrupted=Изворна датотека је оштећена
SourceDoesntExist=Изворна датотека "%1" не постоји
SourceVerificationFailed=Провјера изворне датотеке није успјела: %1
VerificationSignatureDoesntExist=Датотека потписа "%1" не постоји
VerificationSignatureInvalid=Датотека потписа "%1" није ваљана
VerificationKeyNotFound=Датотека потписа "%1" користи непознати кључ
VerificationFileNameIncorrect=Име датотеке је неисправно
VerificationFileTagIncorrect=Ознака датотеке је неисправна
VerificationFileSizeIncorrect=Величина датотеке је неисправно
VerificationFileHashIncorrect=Кодирање датотеке је неисправно
ExistingFileReadOnly2=Постојећу датотеку није било могуће заменити, јер је означена са "samo-za-čitanje".
ExistingFileReadOnlyRetry=&Уклони својство "samo-za-čitanje" и покушај поново
ExistingFileReadOnlyKeepExisting=&Задржи постојећу датотеку
ErrorReadingExistingDest=Грешка приликом покушаја читања постојеће датотеке:
FileExistsSelectAction=Одабери радњу
FileExists2=Датотека већ постоји.
FileExistsOverwriteExisting=&Препиши постојећу датотеку
FileExistsKeepExisting=&Задржи постојећу датотеку
FileExistsOverwriteOrKeepAll=&Уради то и у наредним случајевима
ExistingFileNewerSelectAction=Одабери радњу
ExistingFileNewer2=Постојећа датотека је новија од оне која се покушава инсталирати.
ExistingFileNewerOverwriteExisting=&Препиши постојећу датотеку
ExistingFileNewerKeepExisting=&Задржи постојећу датотеку (препоручено)
ExistingFileNewerOverwriteOrKeepAll=&Уради то и у наредним случајевима
ErrorChangingAttr=Грешка приликом покушаја промене својства постојеће датотеке:
ErrorCreatingTemp=Грешка приликом покушаја стварања датотеке у одредишној фасцикли:
ErrorReadingSource=Грешка приликом покушаја читања изворишне датотеке:
ErrorCopying=Грешка приликом покушаја копирања датотеке:
ErrorDownloading=Грешка приликом преузимања датотеке:
ErrorExtracting=Грешка приликом покушаја распакиравања архиве:
ErrorReplacingExistingFile=Грешка приликом покушаја замењивања датотеке:
ErrorRestartReplace=Замењивање након поновог покретања није успјело:
ErrorRenamingTemp=Грешка приликом покушаја преименовања датотеке у одредишној фасцикли:
ErrorRegisterServer=Није могуће регистрирати DLL/OCX: %1
ErrorRegSvr32Failed=Грешка у RegSvr32. Излазни код %1
ErrorRegisterTypeLib=Није могуће регистрирати библиотеку врста: %1

; *** Uninstall display name markings
; used for example as 'My Program (32-bit)'
UninstallDisplayNameMark=%1 (%2)
; used for example as 'My Program (32-bit, All users)'
UninstallDisplayNameMarks=%1 (%2, %3)
UninstallDisplayNameMark32Bit=32-битни
UninstallDisplayNameMark64Bit=64-битни
UninstallDisplayNameMarkAllUsers=Сви корисници
UninstallDisplayNameMarkCurrentUser=Тренутни корисник

; *** Post-installation errors
ErrorOpeningReadme=Грешка приликом покушаја отварања README датотеке.
ErrorRestartingComputer=Инсталација није могла поново покренути рачунаро. Учини то ручно.

; *** Uninstaller messages
UninstallNotFound=Датотека "%1" не постоји. Уклањање није могуће.
UninstallOpenError=Датотеку "%1" није било могуће отворити. Уклањање није могуће
UninstallUnsupportedVer=Скрипта за уклањање %1 није у облику ове иначице програма за уклањање. Уклањање није могуће
UninstallUnknownEntry=Пронађен је непознат запис (%1) у скрипти за уклањање
ConfirmUninstall=Желите и заиста уклонити %1 и све припадајуће компоненте?
UninstallOnlyOnWin64=Уклањање је могуће само на 64-битном суставу.
OnlyAdminCanUninstall=Уклањање може направити само администратор.
UninstallStatusLabel=Молимо причекајте да се уклањање апликације заврши.
UninstalledAll=%1 је успешно уклоњен с рачунара.
UninstalledMost=Уклањање апликације %1 је завршено.%n%nНеке елементе није било могуће уклонити. Они се могу уклонити ручно.
UninstalledAndNeedsRestart=За завршетак уклањања апликације %1 потребно је поново покренути рачунаро.%n%nНаправити то одмах?
UninstallDataCorrupted="%1" датотека је оштећена. Уклањање није могуће

; *** Uninstallation phase messages
ConfirmDeleteSharedFileTitle=Уклонити дељене датотеке?
ConfirmDeleteSharedFile2=Ова дељена датотека се не користи. Желите ли је уклонити?%n%nАко је неки програми ипак користе можда неће исправнорадити. Ако је не уклоните датотека вам неће сметати за даљњи рад.
SharedFileNameLabel=Датотека:
SharedFileLocationLabel=Место:
WizardUninstalling=Стање Уклањања
StatusUninstalling=%1 уклањање...

; *** Shutdown block reasons
ShutdownBlockReasonInstallingApp=%1 инсталирање.
ShutdownBlockReasonUninstallingApp=%1 деинсталирање.

; The custom messages below aren't used by Setup itself, but if you make
; use of them in your scripts, you'll want to translate them.

[CustomMessages]

NameAndVersion=%1 иначица %2
AdditionalIcons=Додатни пречаци:
CreateDesktopIcon=Створи пречац на ра&дној површини
CreateQuickLaunchIcon=Створи пречац у траци за &брзо покретање
ProgramOnTheWeb=%1 на интернету
UninstallProgram=Уклони %1
LaunchProgram=Покрени %1
AssocFileExtension=&Повежи програм %1 с датотечним наставком %2
AssocingFileExtension=Повезивање програма %1 с датотечним наставком %2 …
AutoStartProgramGroupDescription=Покретање:
AutoStartProgram=Аутоматски покрени %1
AddonHostProgramNotFound=%1 није пронађен у изабраној фасцикли.%n%nЖелите ли свеједно наставити?
