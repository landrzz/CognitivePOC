using System;
using System.IO;
using System.Windows.Input;
using Acr.UserDialogs;
using Microsoft.ProjectOxford.Face;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using Xamarin.Forms;
using System.Linq;
using System.Threading.Tasks;
using Plugin.Media;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Common;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using PropertyChanged;
using System.Runtime.CompilerServices;

namespace CognitivePOC_PCL.PageModels 
{
    [ImplementPropertyChanged]
    public class CognitivePOC_PCLPageModel : FreshMvvm.FreshBasePageModel
    {
        private IUserDialogs _userDialoags;
        ICommand takePhotoCommand;
        ICommand pickPhotoCommand;
        ICommand resetCommand;
        ICommand identifyCommand;
        const string newPhoto = "Take New Photo";
        const string selectPhoto = "Select from Camera Roll";
        public ImageSource ProfileImageSource { get; set; }
        public Stream profileimage { get; set; }
        EmotionServiceClient emotionClient;
        Plugin.Media.Abstractions.MediaFile file;
        string personGroupId;
        public bool SpinnerOn
        {
            get;
            set;
        }
        public string EmotionLabel { get; set; }
        public string EmotionEmojiLabel { get; set; }

        public ImageSource setImage { get; set; }

        ObservableCollection<Person> persons;
        public ObservableCollection<Person> Persons
        {
            get { return persons; }
            set { persons = value;  }
        }

        public CognitivePOC_PCLPageModel(IUserDialogs userDialogs)
        {
            this._userDialoags = userDialogs;
            emotionClient = new EmotionServiceClient("0b9026789fa64a7e84dc5d63b55eddf2");

            Persons = new ObservableCollection<Person>
            {
                new Person { FirstName = "Alex", LastName = "Landers", PhotoURL = "https://thumb.ibb.co/jVy1kv/IMG_3458.jpg" },
                new Person { FirstName = "Alli", LastName = " Maness", PhotoURL = "https://thumb.ibb.co/hiJ1kv/IMG_3484.png" },
                new Person { FirstName = "Ben", LastName = "Landers", PhotoURL = "https://thumb.ibb.co/e3oOyF/IMG_3490.png" },
                new Person { FirstName = "Mark", LastName = "Landers", PhotoURL = "https://thumb.ibb.co/cKQgkv/IMG_3435.jpg" },
                new Person { FirstName = "Melody", LastName = "Beam", PhotoURL = "https://thumb.ibb.co/imo1kv/IMG_3489.jpg" },
            };

          
        }

        protected override  void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
        }

        public ICommand TakePhotoCommand
        {
            get
            {
                return takePhotoCommand ?? (takePhotoCommand = new Command(async () =>
                {
                    var cameraStatus = await CrossPermissions.Current.CheckPermissionStatusAsync(Permission.Camera);
                    var storageStatus = await CrossPermissions.Current.CheckPermissionStatusAsync(Permission.Storage);
                    if (cameraStatus != PermissionStatus.Granted || storageStatus != PermissionStatus.Granted)   // || storageStatus != PermissionStatus.Granted
                    {
                        var results = await CrossPermissions.Current.RequestPermissionsAsync(new[] { Permission.Camera, Permission.Storage }); //, Permission.Storage 
                        cameraStatus = results[Permission.Camera];
                        storageStatus = results[Permission.Storage];
                    }

                    await OpenPhotoTaker();
                }));
            }
        }

        public ICommand PickPhotoCommand
        {
            get
            {
                return pickPhotoCommand ?? (pickPhotoCommand = new Command(async () =>
                {
                    var cameraStatus = await CrossPermissions.Current.CheckPermissionStatusAsync(Permission.Camera);
                    var storageStatus = await CrossPermissions.Current.CheckPermissionStatusAsync(Permission.Storage);
                    if (cameraStatus != PermissionStatus.Granted || storageStatus != PermissionStatus.Granted)   // || storageStatus != PermissionStatus.Granted
                    {
                        var results = await CrossPermissions.Current.RequestPermissionsAsync(new[] { Permission.Camera, Permission.Storage }); //, Permission.Storage 
                        cameraStatus = results[Permission.Camera];
                        storageStatus = results[Permission.Storage];
                    }

                    await OpenPhotoSelecter();
                }));
            }
        }

        public ICommand ResetCommand
        {
            get
            {
                return resetCommand ?? (resetCommand = new Command( () =>
                {
                    ProfileImageSource = null;
                    setImage = ProfileImageSource;

                    EmotionLabel= "";
                    EmotionEmojiLabel = "";
                }));
            }
        }

        public ICommand IdentifyCommand
        {
            get
            {
                return identifyCommand ?? (identifyCommand = new Command(async () =>
                {
                    ActivateSpinner();
                    try
                    {
						await RegisterEmployees();

						await ExecuteFindSimilarFaceCommandAsync();

                    } catch (Exception ex) { Debug.WriteLine(ex.Message+"  "+ex.InnerException);}
                    DeactivateSpinner();
                }));
            }
        }




        public async Task OpenPhotoTaker()
        {
            ActivateSpinner();

            var rnd = new Random();
            var rndEnding = rnd.Next(100, 9999);
            if (!CrossMedia.Current.IsCameraAvailable || !CrossMedia.Current.IsTakePhotoSupported)
            {
                await _userDialoags.AlertAsync("It appears that no camera is available", "No Camera", "OK");
                return;
            }
            var myfile = await CrossMedia.Current.TakePhotoAsync(new Plugin.Media.Abstractions.StoreCameraMediaOptions
            {
                PhotoSize = Plugin.Media.Abstractions.PhotoSize.Small,                 //resizes the photo to 50% of the original
                CompressionQuality = 92,                                                // Int from 0 to 100 to determine image compression level
                DefaultCamera = Plugin.Media.Abstractions.CameraDevice.Front,           // determine which camera to default to
                Directory = "CC Directors",
                Name = $"myprofilepic{rndEnding}",
                SaveToAlbum = true,                                                     // this saves the photo to the camera roll 
                //if we need the public album path --> var aPpath = file.AlbumPath;
                //if we need a private path --> var path = file.Path;
                AllowCropping = false,
            });
            file = myfile;
            if (file == null)
            {
                DeactivateSpinner();
                return;
            }

            Debug.WriteLine("File Location: " + file.Path + "     <--- here");


            // imageChanged = true;
           setImage = ImageSource.FromStream(() =>
            {
                var stream = file.GetStream();
                var path = file.Path;
                var pathprivate = file.AlbumPath;
                Debug.WriteLine(path.ToString());
                Debug.WriteLine(pathprivate.ToString());
                profileimage = file.GetStream();


                //file.Dispose();
                return stream;
            });

            await DetermineEmotion();



            DeactivateSpinner();
        }




        public async Task OpenPhotoSelecter()
        {
            ActivateSpinner();

            if (!CrossMedia.Current.IsPickPhotoSupported)
            {
                await _userDialoags.AlertAsync("Permission was not granted to access camera roll or picking photos is not available on this device.", "Cannot Pick Photo", "OK");
                return;
            }
            var myfile = await CrossMedia.Current.PickPhotoAsync(new Plugin.Media.Abstractions.PickMediaOptions
            {
                PhotoSize = Plugin.Media.Abstractions.PhotoSize.Small, 
                CompressionQuality = 92, 
            });
            file = myfile;
            if (file == null)
            {
                DeactivateSpinner();
                return;
            }
            string filePath = file.Path;
            string fileType = filePath.Substring(filePath.Length - 4);
            Debug.WriteLine($"****    {fileType}   *****");
            if (fileType == ".jpg" || fileType == ".png" || fileType == ".JPG" || fileType == ".PNG")
            {
                //imageChanged = true;
                setImage = ImageSource.FromStream(() =>
                {
                    var stream = file.GetStream();
                    var path = file.Path;
                    var pathprivate = file.AlbumPath;
                    Debug.WriteLine(path);
                    Debug.WriteLine(pathprivate);
                    profileimage = file.GetStream();
                    //file.Dispose();
                    return stream;
                });

            }
            else
            {
                await _userDialoags.AlertAsync("Unsupported file type.", "Error", "OK");
            }

            await DetermineEmotion();

            DeactivateSpinner();
        }





        public async Task DetermineEmotion()
        {
            try
            {
                if (file != null)
                {
                    using (var photoStream = file.GetStream())
                    {

                        var emotionResult = await emotionClient.RecognizeAsync(photoStream);
                        if (emotionResult.Any())
                        {
                            // Emotions detected are happiness, sadness, surprise, anger, fear, contempt, disgust, or neutral.
                            var myListOfEmotions = new ObservableCollection<string>();

                            foreach (var face in emotionResult)
                            {
                                var emotionType = face.Scores.ToRankedList().FirstOrDefault().Key;
                                myListOfEmotions.Add(emotionType);
                            }

                            var myListOfEmojis = new List<string>();

                            foreach (var face in emotionResult)
                            {
                                var emotionType = face.Scores.ToRankedList().FirstOrDefault().Key;
                                var emoji = string.Empty;
                                switch (emotionType)
                                {
                                    case "Happiness":
                                        //emojiLabel.Text = "\U0001F601";
                                        emoji = "\U0001F601";
                                        break;
                                    case "Sadness":
                                        emoji =  "\U0001F641";
                                        break;
                                    case "Surprise":
                                        emoji = "\U0001F632";
                                        break;
                                    case "Anger":
                                        emoji = "\U0001F621";
                                        break;
                                    case "Fear":
                                        emoji = "\U0001F628";
                                        break;
                                    case "Contempt":
                                        emoji = "\U0001F642";
                                        break;
                                    case "Disgust":
                                        emoji = "\U0001F616";
                                        break;
                                    case "Neutral":
                                        emoji = "\U0001F636";
                                        break;
                                }

                                myListOfEmojis.Add(emoji);
                            }


                            //var emotionType  = emotionResult.FirstOrDefault().Scores.ToRankedList().FirstOrDefault().Key;
                            EmotionLabel = string.Join(", ", myListOfEmotions.ToList());         //emotionType;
                            EmotionEmojiLabel = string.Join("  ", myListOfEmojis);

                        }
                        else 
                        {
                            EmotionLabel = "Try taking a better picture.";
                        }
                        //file.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }


        }










        async Task RegisterEmployees()
        {
            var faceServiceClient = new FaceServiceClient("8f1c2baf6778445f94c3effc4ed53786");

            // Step 1 - Create Person Group
            var personGroup = Guid.NewGuid().ToString();
          

            await faceServiceClient.CreatePersonGroupAsync(personGroupId, "My Peeps");

            // Step 2 - Add persons (and faces) to person group.
            foreach (var p in Persons)
            {
                // Step 2a - Create a new person, identified by their name.
                var disPerson = await faceServiceClient.CreatePersonAsync(personGroupId, string.Format(p.FirstName + " " + p.LastName));
                // Step 3a - Add a face for that person.
                await faceServiceClient.AddPersonFaceAsync(personGroupId, disPerson.PersonId, p.PhotoURL);
            }

            // Step 3 - Train facial recognition model.
            await faceServiceClient.TrainPersonGroupAsync(personGroupId);
        }





        Command findSimilarFaceCommand;
        public Command FindSimilarFaceCommand
        {
            get { return findSimilarFaceCommand ?? (findSimilarFaceCommand = new Command(async () => await ExecuteFindSimilarFaceCommandAsync())); }
        }

        async Task ExecuteFindSimilarFaceCommandAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            ActivateSpinner();

            try
            {
                if (file != null)
                {
                    using (var stream = file.GetStream())
                    {
                        var faceServiceClient = new FaceServiceClient("8f1c2baf6778445f94c3effc4ed53786");

                        // Step 4a - Detect the faces in this photo.
                        var faces = await faceServiceClient.DetectAsync(stream);
                        var faceIds = faces.Select(face => face.FaceId).ToArray();

                        // Step 4b - Identify the person in the photo, based on the face.
                        var results = await faceServiceClient.IdentifyAsync(personGroupId, faceIds);
                        var result = results[0].Candidates[0].PersonId;

                        // Step 4c - Fetch the person from the PersonId and display their name.
                        var person = await faceServiceClient.GetPersonAsync(personGroupId, result);
                        UserDialogs.Instance.ShowSuccess($"Person identified is {person.FirstName} {person.LastName}.");
                    }
                }

            }
            catch (Exception ex)
            {
                UserDialogs.Instance.ShowError(ex.Message);
            }
            finally
            {
                IsBusy = false;
                DeactivateSpinner();
            }
        }

       







        public void ActivateSpinner()
        {
            //spinner.IsVisible = true;
            //spinner.IsEnabled = true;
            //spinner.IsRunning = true;
            SpinnerOn = true;
            RaisePropertyChanged();
        }

        public void DeactivateSpinner()
        {
            //spinner.IsVisible = false;
            //spinner.IsEnabled = false;
            //spinner.IsRunning = false;
            SpinnerOn = false;
        }











        /// <summary>
        /// Gets or sets a value indicating whether this instance is busy.
        /// </summary>
        /// <value><c>true</c> if this instance is busy; otherwise, <c>false</c>.</value>
        bool isBusy;
        public bool IsBusy
        {
            get { return isBusy; }
            set
            {
                if (SetProperty(ref isBusy, value))
                    IsNotBusy = !isBusy;
            }
        }
        /// <summary>
        /// Gets or sets a value indicating whether this instance is not busy.
        /// </summary>
        /// <value><c>true</c> if this instance is not busy; otherwise, <c>false</c>.</value>
        bool isNotBusy = true;
        public bool IsNotBusy
        {
            get { return isNotBusy; }
            private set { SetProperty(ref isNotBusy, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is busy.
        /// </summary>
        /// <value><c>true</c> if this instance is busy; otherwise, <c>false</c>.</value>
        bool isBusyRefreshing;
        public bool IsBusyRefreshing
        {
            get { return isBusyRefreshing; }
            set
            {
                if (SetProperty(ref isBusyRefreshing, value))
                    IsNotBusyRefreshing = !isBusyRefreshing;
            }
        }
        /// <summary>
        /// Gets or sets a value indicating whether this instance is not busy.
        /// </summary>
        /// <value><c>true</c> if this instance is not busy; otherwise, <c>false</c>.</value>
        bool isNotBusyRefreshing = true;
        public bool IsNotBusyRefreshing
        {
            get { return isNotBusyRefreshing; }
            private set { SetProperty(ref isNotBusyRefreshing, value); }
        }

        //does the whole SetProperty thing.... not quite sure.
        protected bool SetProperty<T>(
            ref T backingStore, T value,
            [CallerMemberName]string propertyName = "",
            Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;
            backingStore = value;
            if (onChanged != null)
                onChanged();
            return true;
        }


    }
}
