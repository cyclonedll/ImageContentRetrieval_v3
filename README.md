### A image content-based retrieval tool
1. Using [Vorcyc.RoundUI](https://www.nuget.org/packages/Vorcyc.RoundUI/) to support GUI.
2. Using __TensorFlow.NET__ and __Google inception V3 classfication model__ to get bottleneck layer data (embedding). The embedding vector is an array of 32-bit floating number in length of 1001.
3. Using __Euclidean distance__ to get simility between images.
4. Using [KEY-VALUE] pair to saving [image filename-feature vector] as data storage by Ash class.
5. Using __Win32 Shell API__ to locate file.