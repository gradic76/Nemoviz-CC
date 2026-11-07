; *** Inno Setup version 6.5.0+ Serbian (Latin) messages ***
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
LanguageName=Srpski
LanguageID=$081a
LanguageCodePage=1250
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
SetupAppTitle=Postavljanje aplikacije
SetupWindowTitle=Postavljanje – %1
UninstallAppTitle=Uklanjanje
UninstallAppFullTitle=Uklanjanje aplikacije %1

; *** Misc. common
InformationTitle=Obaveštenje
ConfirmTitle=Potvrda
ErrorTitle=Greška

; *** SetupLdr messages
SetupLdrStartupMessage=Ovime će se instalirati %1. Želiš li nastaviti?
LdrCannotCreateTemp=Nije moguće stvoriti privremenu datoteku. Instalacija je prekinuta
LdrCannotExecTemp=Nije moguće pokrenuti datoteku u privremenoj fascikli. Instalacija je prekinuta
HelpTextNote=

; *** Startup error messages
LastErrorMessage=%1.%n%nnGreška %2: %3
SetupFileMissing=Datoteka %1 se ne nalazi u fascikli instalacije. Ispravi problem ili nabavi novu kopiju programa.
SetupFileCorrupt=Datoteke instalacije su oštećene. Nabavi novu kopiju programa.
SetupFileCorruptOrWrongVer=Datoteke instalacije su oštećene ili nisu kompatibilne s ovom verzijom instalacije. Ispravi problem ili nabavi novu kopiju programa.
InvalidParameter=Neispravan parametar je prenet u naredbenom retku:%n%n%1
SetupAlreadyRunning=Instalacija je već pokrenuta.
WindowsVersionNotSupported=Program ne podržava Windows verziju koju koristiš.
WindowsServicePackRequired=Program zahteva %1 servisni paket %2 ili noviji.
NotOnThisPlatform=Program neće raditi na %1.
OnlyOnThisPlatform=Program se mora pokrenuti na %1.
OnlyOnTheseArchitectures=Program se može instalirati na Windows verzijama za sledeće procesorske arhitekture:%n%n%1
WinVersionTooLowError=Program zahteva %1 verziju %2 ili noviju.
WinVersionTooHighError=Program se ne može instalirati na %1 verziji %2 ili novijoj.
AdminPrivilegesRequired=Za instaliranje programa moraš biti prijavljen/a kao administrator.
PowerUserPrivilegesRequired=Za instaliranje programa moraš biti prijavljen/a kao administrator ili kao član grupe naprednih korisnika.
SetupAppRunningError=Instalacija je otkrila da je %1 trenutačno pokrenut.%n%nZatvori program i potom pritisni "Dalje" za nastavak ili "Odustani" za prekid.
UninstallAppRunningError=Deinstalacija je otkrila da je %1 trenutačno pokrenut.%n%nZatvori program i potom pritisni "Dalje" za nastavak ili "Odustani" za prekid.

; *** Startup questions
PrivilegesRequiredOverrideTitle=Odaberi način instaliranja
PrivilegesRequiredOverrideInstruction=Odaberi način postavljanja
PrivilegesRequiredOverrideText1=%1 može se postaviti za vas ili za sve korisnike (potrebna su administratorska prava).
PrivilegesRequiredOverrideText2=%1 može se postaviti za vas ili za sve korisnike (potrebna su administratorska prava).
PrivilegesRequiredOverrideAllUsers=Post&avi za sve korisnike
PrivilegesRequiredOverrideAllUsersRecommended=Post&avi za sve korisnike (preporučeno)
PrivilegesRequiredOverrideCurrentUser=Postavi samo za &mene
PrivilegesRequiredOverrideCurrentUserRecommended=Postavi samo za &mene (preporučeno)

; *** Misc. errors
ErrorCreatingDir=Instalacija nije mogla stvoriti fasciklu "%1"
ErrorTooManyFilesInDir=Datoteku nije moguće stvoriti u fascikli "%1", jer fascikla sadrži previše datoteka

; *** Setup common messages
ExitSetupTitle=Zaustavi postavljanje
ExitSetupMessage=Postavljanje nije završeno. Ako sada prekinete aplikacija se neće postaviti.%n%nPostavljanje možete završiti kasnije.%n%nPrekinuti sada?
AboutSetupMenuItem=&O postavljanju...
AboutSetupTitle=O postavljanju
AboutSetupMessage=%1 Inačica %2%n%3%n%n%1 Web stranica:%n%4
AboutSetupNote=
TranslatorNote=Prevoditelji:%n%nKrunoslav Kanjuh%n%nElvis Gambiraža%n%nMilo Ivir%n%nGordan Radić

; *** Buttons
ButtonBack=Pre&thodno
ButtonNext=Sle&deće
ButtonInstall=Postav&i
ButtonOK=U redu
ButtonCancel=Otkaži
ButtonYes=&Da
ButtonYesToAll=D&a za sve
ButtonNo=&Ne
ButtonNoToAll=N&e za sve
ButtonFinish=&Završi
ButtonBrowse=&Pregledaj...
ButtonWizardBrowse=P&regledaj...
ButtonNewFolder=&Stvori novu fasciklu

; *** "Select Language" dialog messages
SelectLanguageTitle=Izaberite jezik postavljanja
SelectLanguageLabel=Izaberite jezik za postupak postavljanja

; *** Common wizard text
ClickNext=Pritisnite Sledeće za nastavak ili otkaži za prekid postavljanja
BeveledLabel=
BrowseDialogTitle=Izaberite fasciklu
BrowseDialogLabel=Izaberite fasciklu iz popisa i pritisnite U redu.
NewFolderName=Nova fascikla

; *** "Welcome" wizard page
WelcomeLabel1=Čarobnjak za postavljanje aplikacije[name]
WelcomeLabel2=Uskoro ćete početi s postavljanjem aplikacije [name/ver].%n%nPreporučujemo da pre sledećeg koraka zatvorite sve aktivne aplikacije.

; *** "Password" wizard page
WizardPassword=Lozinka
PasswordLabel1=Instalacija je zaštićena lozinkom.
PasswordLabel3=Upiši lozinku i pritisni "Dalje". Lozinke su osjetljive na mala i velika slova.
PasswordEditLabel=&Lozinka:
IncorrectPassword=Upisana je pogrešna lozinka. Pokušaj ponovo.

; *** "License Agreement" wizard page
WizardLicense=Licencni ugovor
LicenseLabel=Molimo pročitajte sledeće podatke pre nastavka.
LicenseLabel3=Molimo pročitajte ugovor. Za nastavak morate prihvatiti uvjete ugovora.
LicenseAccepted=&Prihvaćam
LicenseNotAccepted=&Ne prihvaćam

; *** "Information" wizard pages
WizardInfoBefore=Obaveštenjei
InfoBeforeLabel=Molimo pročitajte sledeće obaveštenja pre nastavka
InfoBeforeClickLabel=Za nastavak pritisnite Sledeće
WizardInfoAfter=Obaveštenjei
InfoAfterLabel=Molimo pročitajte sledeće obaveštenja pre nastavka
InfoAfterClickLabel=Za nastavak pritisnite Sledeće

; *** "User Information" wizard page
WizardUserInfo=Korisnički podaci
UserInfoDesc=Upiši svoje podatke.
UserInfoName=&Ime korisnika:
UserInfoOrg=&Organizacija:
UserInfoSerial=&Serijski broj:
UserInfoNameRequired=Ime je obavezno polje.

; *** "Select Destination Location" wizard page
WizardSelectDir=Izaberite fasciklu za postavljanje
SelectDirDesc=Gdje želite postaviti [name]?
SelectDirLabel3=[name] će se postaviti u ovu fasciklu:
SelectDirBrowseLabel=Za nastavak postavljanja pritisnite Sledeće. Za odabir druge fascikle pritisnite Pregledaj.
DiskSpaceGBLabel=Potrebno je barem [gb] GB slobodnog prostora na disku.
DiskSpaceMBLabel=Potrebno je barem [mb] MB slobodnog prostora na disku.
CannotInstallToNetworkDrive=Aplikacija se ne može postaviti na mrežni pogon
CannotInstallToUNCPath=Aplikacija se ne može postaviti na UNC putanju
InvalidPath=Morate upisati punu putanju sa slovom pogona, npr:%n%nC:\APP%n%n ili UNC putanju u obliku: %n%n\\poslužitelj\share
InvalidDrive=Izabrani pogon ne postoji. Izaberite drugi.
DiskSpaceWarningTitle=Nedovoljno prostora na izabranom pogonu
DiskSpaceWarning=Za postavljanje je potrebno barem %1 KB slobodnog prostora, no izabrani pogon ima samo %2 KB.%n%n Svejedno nastaviti?
DirNameTooLong=Putanja ili ime fascikle su predugački
InvalidDirName=Ime fascikle je neispravno
BadDirName32=Ime fascikle ne sme sadržavati sledeće znakove:%n%n%1
DirExistsTitle=Fascikla već postoji
DirExists=Fascikla:%n%n%1%n%nveć postoji. Svejedno postaviti?
DirDoesntExistTitle=Fascikla ne postoji
DirDoesntExist=Fascikla:%n%n%1%n%nne postoji. Želite li je stvoriti?

; *** "Select Components" wizard page
WizardSelectComponents=Odaberi komponente
SelectComponentsDesc=Koje komponente želiš instalirati?
SelectComponentsLabel2=Odaberi komponente koje želiš instalirati, isključi komponente koje ne želiš instalirati. Za nastavak instalacije pritisni "Dalje".
FullInstallation=Kompletna instalacija
; if possible don't translate 'Compact' as 'Minimal' (I mean 'Minimal' in your language)
CompactInstallation=Kompaktna instalacija
CustomInstallation=Prilagođena instalacija
NoUninstallWarningTitle=Postojeće komponente
NoUninstallWarning=Instalacija je utvrdila da na tvom računaru već postoje sledeće komponente:%n%n%1%n%nIsključivanjem tih komponenata, one se neće deinstalirati.%n%nŽeliš li svejedno nastaviti?
ComponentSize1=%1 KB
ComponentSize2=%1 MB
ComponentsDiskSpaceGBLabel=Trenutačni odabir zahteva barem [gb] GB na disku.
ComponentsDiskSpaceMBLabel=Trenutačni odabir zahteva barem [mb] MB na disku.

; *** "Select Additional Tasks" wizard page
WizardSelectTasks=Izaberite sledeće postupke
SelectTasksDesc=Koje dodatne postupke želite napraviti?
SelectTasksLabel2=Izaberite postupke koji će se izvršiti prilikom postavljanja aplikacije [name], a zatim pritisnite Sledeće.

; *** "Select Start Menu Folder" wizard page
WizardSelectProgramGroup=Izaberite fasciklu u meniu Start
SelectStartMenuFolderDesc=Gdje želite postaviti prečace aplikacije?
SelectStartMenuFolderLabel3=Prečaci aplikacije postavit će se u ovu fasciklu menia Start
SelectStartMenuFolderBrowseLabel=Za nastavak pritisnite Sledeće, za odabir druge fascikle pritisnite Pregledaj
MustEnterGroupName=Ime fascikle je obavezno
GroupNameTooLong=Putanja ili ime fascikle su predugački
InvalidGroupName=Ime fascikle nije ispravno
BadGroupName=Ime fascikle ne sme sadržavati sledeće znakove:%n%n%1
NoProgramGroupCheck2=&Nemoj stvoriti fasciklu u meniu Start

; *** "Ready to Install" wizard page
WizardReady=Sve je spremno za postavljanje
ReadyLabel1=Sve je spremno za postavljanje aplikacije[name].
ReadyLabel2a=Za postavljanje pritisnite Postavi. Za provjeru opcija postavljanja pritisnite Natrag
ReadyLabel2b=Za postavljanje pritisnite Postavi
ReadyMemoUserInfo=Korisnički podaci:
ReadyMemoDir=Odredišno mesto:
ReadyMemoType=Način postavljanja:
ReadyMemoComponents=Izabrane komponente:
ReadyMemoGroup=Fascikla u meniu Start:
ReadyMemoTasks=Dodatni zadaci:

; *** TDownloadWizardPage wizard page and DownloadTemporaryFile
DownloadingLabel2=Preuzimanje datoteka …
ButtonStopDownload=&Prekini preuzimanje
StopDownload=Stvarno želiš prekinuti preuzimanje?
ErrorDownloadAborted=Preuzimanje je prekinuto
ErrorDownloadFailed=Neuspjelo preuzimanje: %1 %2
ErrorDownloadSizeFailed=Neuspjelo dohvaćanje veličine: %1 %2
ErrorProgress=Neispravan napredak: %1 od %2
ErrorFileSize=Neispravna veličina datoteke: očekivano %1, pronađeno %2

; *** TExtractionWizardPage wizard page and Extract7ZipArchive
ExtractingLabel=Raspakiravanje datoteka …
ButtonStopExtraction=&Prekini raspakiravanje
StopExtraction=Stvarno želiš prekinuti raspakiravanje?
ErrorExtractionAborted=Raspakiravanje prekinuto
ErrorExtractionFailed=Raspakiravanje neuspjelo: %1

; *** Archive extraction failure details
ArchiveIncorrectPassword=Lozinka je neispravna
ArchiveIsCorrupted=Arhiva je pokvarena
ArchiveUnsupportedFormat=Format arhive nije podržan

; *** "Preparing to Install" wizard page
WizardPreparing=Priprema postavljanja
PreparingDesc=Pripremamo postavljanje aplikacije[name].
PreviousInstallNotCompleted=Postavljanje ili uklanjanje prethodne aplikacije nije završilo. Morate ponovo pokrenuti računaro i završiti postavljanje.%n%n Nakon ponovog pokretanja računara pokrenite opet postavljanje aplikacije [name].
CannotContinue=Postavljanje ne može nastaviti. Pritisnite Izlaz za prekidanje postavljanja.
ApplicationsFound=Ovi programi koriste datoteke koje postavljanje mora ažurirati. Predlažemo da zatvorite navedene programe.
ApplicationsFound2=Ovi programi koriste datoteke koje postavljanje mora ažurirati. Predlažemo da zatvorite navedene programe. Kad postavljanje završi pokušat ćemo ih ponovo pokrenuti.
CloseApplications=&Zatvori automatski
DontCloseApplications=&Ne zatvaraj
ErrorCloseApplications=Nismo uspjeli zatvoriti sve programe automatski. Molimo da pokušate zatvoriti programe ručno.
PrepareToInstallNeedsRestart=Potrebnoje ponovo pokrenuti računaro. Nakon toga ponovo pokrenite postavljanje aplikacije [name] kako bismo nastavili postavljanje.%n%nPonovo pokrenuti računaro?

; *** "Installing" wizard page
WizardInstalling=Postavljanje
InstallingLabel=Molimo pričekajte...

; *** "Setup Completed" wizard page
FinishedHeadingLabel=Završavanje čarobnjaka za postavljanje [name]
FinishedLabelNoIcons=Aplikacija [name] je uspešno postavljena.
FinishedLabel=Aplikacija [name] je uspešno postavljena. Možete je pokrenuti na postavljenim prečacima.
ClickFinish=Za dovršavanje postavjanja pritisnite Završi.
FinishedRestartLabel=Za završetak postavljanja potrebno je ponovo pokrenuti računaro.%n%nNapraviti to odmah?
FinishedRestartMessage=Za završetak postavljanja potrebno je ponovo pokrenuti računaro.%n%nNapraviti to odmah?
ShowReadmeCheck=Da, želim pročitati README datoteku
YesRadio=&Da, ponovo pokreni
NoRadio=&Ne, napravit ću to kasnije
; used for example as 'Run MyProg.exe'
RunEntryExec=Pokreni %1
; used for example as 'View Readme.txt'
RunEntryShellExec=Prikaži %1

; *** "Setup Needs the Next Disk" stuff
ChangeDiskTitle=Instalacija treba sledeći disk
SelectDiskLabel2=Umetni disk %1 i pritisni "U redu".%n%nAko se datoteke s ovog diska nalaze na nekom drugom mestu od dolje prikazanog, upiši ispravnu stazu ili pritisni "Odaberi".
PathLabel=&Staza:
FileNotInDir2=Staza "%1" ne postoji u "%2". Umetni odgovarajući disk ili odaberi jednu drugu fasciklu.
SelectDirectoryLabel=Odredi mesto sledećeg diska.

; *** Installation phase messages
SetupAborted=Postavljanje nije dovršeno.%n%nIspravite problem i pokušajte ponovo.
AbortRetryIgnoreSelectAction=Izaberite radnju
AbortRetryIgnoreRetry=&Pokušaj ponovo
AbortRetryIgnoreIgnore=&Zanemari grešku i nastavi
AbortRetryIgnoreCancel=Prekini postavljanje
RetryCancelSelectAction=Odaberi radnju
RetryCancelRetry=&Pokušaj ponovo
RetryCancelCancel=Otkaži

; *** Installation status messages
StatusClosingApplications=Zatvaranje programa …
StatusCreateDirs=Stvaranje fascikli …
StatusExtractFiles=Raspakiranje datoteka …
StatusDownloadFiles=Preuzimanje datoteka …
StatusCreateIcons=Stvaranje prečaca …
StatusCreateIniEntries=Stvaranje INI unosa …
StatusCreateRegistryEntries=Stvaranje unosa u registar …
StatusRegisterFiles=Registriranje datoteka …
StatusSavingUninstall=Spremanje podataka za uklanjanje...
StatusRunProgram=Završavanje postavljanja
StatusRestartingApplications=Ponovo pokretanje programa …
StatusRollback=Poništavanje promena …

; *** Misc. errors
ErrorInternal2=Interna greška: %1
ErrorFunctionFailedNoCode=%1 – neuspjelo
ErrorFunctionFailed=%1 – neuspjelo; kod %2
ErrorFunctionFailedWithMessage=%1 – neuspjelo; kod %2.%n%3
ErrorExecutingProgram=Nije moguće izvršiti datoteku:%n%1

; *** Registry errors
ErrorRegOpenKey=Greška prilikom otvaranja ključa registra:%n%1\%2
ErrorRegCreateKey=Greška prilikom stvaranja ključa registra:%n%1\%2
ErrorRegWriteKey=Greška prilikom pisanja u ključ registra:%n%1\%2

; *** INI errors
ErrorIniEntry=Greška prilikom stvaranja INI unosa u datoteci "%1".

; *** File copying errors
FileAbortRetryIgnoreSkipNotRecommended=&Preskoči ovu datoteku (ne preporučuje se)
FileAbortRetryIgnoreIgnoreNotRecommended=&Zanemari grešku i nastavi (ne preporučuje se)
SourceIsCorrupted=Izvorna datoteka je oštećena
SourceDoesntExist=Izvorna datoteka "%1" ne postoji
SourceVerificationFailed=Provjera izvorne datoteke nije uspjela: %1
VerificationSignatureDoesntExist=Datoteka potpisa "%1" ne postoji
VerificationSignatureInvalid=Datoteka potpisa "%1" nije valjana
VerificationKeyNotFound=Datoteka potpisa "%1" koristi nepoznati ključ
VerificationFileNameIncorrect=Ime datoteke je neispravno
VerificationFileTagIncorrect=Oznaka datoteke je neispravna
VerificationFileSizeIncorrect=Veličina datoteke je neispravno
VerificationFileHashIncorrect=Kodiranje datoteke je neispravno
ExistingFileReadOnly2=Postojeću datoteku nije bilo moguće zameniti, jer je označena sa "samo-za-čitanje".
ExistingFileReadOnlyRetry=&Ukloni svojstvo "samo-za-čitanje" i pokušaj ponovo
ExistingFileReadOnlyKeepExisting=&Zadrži postojeću datoteku
ErrorReadingExistingDest=Greška prilikom pokušaja čitanja postojeće datoteke:
FileExistsSelectAction=Odaberi radnju
FileExists2=Datoteka već postoji.
FileExistsOverwriteExisting=&Prepiši postojeću datoteku
FileExistsKeepExisting=&Zadrži postojeću datoteku
FileExistsOverwriteOrKeepAll=&Uradi to i u narednim slučajevima
ExistingFileNewerSelectAction=Odaberi radnju
ExistingFileNewer2=Postojeća datoteka je novija od one koja se pokušava instalirati.
ExistingFileNewerOverwriteExisting=&Prepiši postojeću datoteku
ExistingFileNewerKeepExisting=&Zadrži postojeću datoteku (preporučeno)
ExistingFileNewerOverwriteOrKeepAll=&Uradi to i u narednim slučajevima
ErrorChangingAttr=Greška prilikom pokušaja promene svojstva postojeće datoteke:
ErrorCreatingTemp=Greška prilikom pokušaja stvaranja datoteke u odredišnoj fascikli:
ErrorReadingSource=Greška prilikom pokušaja čitanja izvorišne datoteke:
ErrorCopying=Greška prilikom pokušaja kopiranja datoteke:
ErrorDownloading=Greška prilikom preuzimanja datoteke:
ErrorExtracting=Greška prilikom pokušaja raspakiravanja arhive:
ErrorReplacingExistingFile=Greška prilikom pokušaja zamenjivanja datoteke:
ErrorRestartReplace=Zamenjivanje nakon ponovog pokretanja nije uspjelo:
ErrorRenamingTemp=Greška prilikom pokušaja preimenovanja datoteke u odredišnoj fascikli:
ErrorRegisterServer=Nije moguće registrirati DLL/OCX: %1
ErrorRegSvr32Failed=Greška u RegSvr32. Izlazni kod %1
ErrorRegisterTypeLib=Nije moguće registrirati biblioteku vrsta: %1

; *** Uninstall display name markings
; used for example as 'My Program (32-bit)'
UninstallDisplayNameMark=%1 (%2)
; used for example as 'My Program (32-bit, All users)'
UninstallDisplayNameMarks=%1 (%2, %3)
UninstallDisplayNameMark32Bit=32-bitni
UninstallDisplayNameMark64Bit=64-bitni
UninstallDisplayNameMarkAllUsers=Svi korisnici
UninstallDisplayNameMarkCurrentUser=Trenutni korisnik

; *** Post-installation errors
ErrorOpeningReadme=Greška prilikom pokušaja otvaranja README datoteke.
ErrorRestartingComputer=Instalacija nije mogla ponovo pokrenuti računaro. Učini to ručno.

; *** Uninstaller messages
UninstallNotFound=Datoteka "%1" ne postoji. Uklanjanje nije moguće.
UninstallOpenError=Datoteku "%1" nije bilo moguće otvoriti. Uklanjanje nije moguće
UninstallUnsupportedVer=Skripta za uklanjanje %1 nije u obliku ove inačice programa za uklanjanje. Uklanjanje nije moguće
UninstallUnknownEntry=Pronađen je nepoznat zapis (%1) u skripti za uklanjanje
ConfirmUninstall=Želite i zaista ukloniti %1 i sve pripadajuće komponente?
UninstallOnlyOnWin64=Uklanjanje je moguće samo na 64-bitnom sustavu.
OnlyAdminCanUninstall=Uklanjanje može napraviti samo administrator.
UninstallStatusLabel=Molimo pričekajte da se uklanjanje aplikacije završi.
UninstalledAll=%1 je uspešno uklonjen s računara.
UninstalledMost=Uklanjanje aplikacije %1 je završeno.%n%nNeke elemente nije bilo moguće ukloniti. Oni se mogu ukloniti ručno.
UninstalledAndNeedsRestart=Za završetak uklanjanja aplikacije %1 potrebno je ponovo pokrenuti računaro.%n%nNapraviti to odmah?
UninstallDataCorrupted="%1" datoteka je oštećena. Uklanjanje nije moguće

; *** Uninstallation phase messages
ConfirmDeleteSharedFileTitle=Ukloniti deljene datoteke?
ConfirmDeleteSharedFile2=Ova deljena datoteka se ne koristi. Želite li je ukloniti?%n%nAko je neki programi ipak koriste možda neće ispravnoraditi. Ako je ne uklonite datoteka vam neće smetati za daljnji rad.
SharedFileNameLabel=Datoteka:
SharedFileLocationLabel=Mesto:
WizardUninstalling=Stanje Uklanjanja
StatusUninstalling=%1 uklanjanje...

; *** Shutdown block reasons
ShutdownBlockReasonInstallingApp=%1 instaliranje.
ShutdownBlockReasonUninstallingApp=%1 deinstaliranje.

; The custom messages below aren't used by Setup itself, but if you make
; use of them in your scripts, you'll want to translate them.

[CustomMessages]

NameAndVersion=%1 inačica %2
AdditionalIcons=Dodatni prečaci:
CreateDesktopIcon=Stvori prečac na ra&dnoj površini
CreateQuickLaunchIcon=Stvori prečac u traci za &brzo pokretanje
ProgramOnTheWeb=%1 na internetu
UninstallProgram=Ukloni %1
LaunchProgram=Pokreni %1
AssocFileExtension=&Poveži program %1 s datotečnim nastavkom %2
AssocingFileExtension=Povezivanje programa %1 s datotečnim nastavkom %2 …
AutoStartProgramGroupDescription=Pokretanje:
AutoStartProgram=Automatski pokreni %1
AddonHostProgramNotFound=%1 nije pronađen u izabranoj fascikli.%n%nŽelite li svejedno nastaviti?
