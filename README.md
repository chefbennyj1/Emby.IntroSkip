# Emby.IntroSkip

Calculate tv episode intro start and end time positions in the stream by encoding audio, fingerprinting, and hamming distances.
Return an object with the episode InternalId, Intro start Timespan, Intro end Timespan and a boolean which can be checked for the existense of a title sequence for the BaseItem.

Linux users currently must give fpcalc elevated rights inorder for it to fingerprint audio from the emby libraries:

```
  cd /config/plugins/configurations/introEncoding/
  chmod +x fpcalc
```


![alt text](https://raw.githubusercontent.com/chefbennyj1/Emby.IntroSkip/master/asset1.png)
![alt text](https://raw.githubusercontent.com/chefbennyj1/Emby.IntroSkip/master/asset2.png)
![alt text](https://raw.githubusercontent.com/chefbennyj1/Emby.IntroSkip/master/asset3.png)
![alt text](https://raw.githubusercontent.com/chefbennyj1/Emby.IntroSkip/master/asset4.png)

