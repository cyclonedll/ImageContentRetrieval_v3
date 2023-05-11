### A image content-based retrieval tool
1. Using Vorcyc.RoundUI WPF UI framework to build GUI
2. Using TensorFlow.NET and Google inception V3 classfication model to get bottleneck layer data (embedding). The embedding vector is an array of 32-bit floating number in length of 1001.
3. Using Euclidean distance to get simility between images.
4. Using [KEY-VALUE] pair to saving [image filename-feature vector] as data storage by Ash class.
5. Using Win32 Shell API to locate file.
.NET 5 with TF.NET and Euclidean distance.