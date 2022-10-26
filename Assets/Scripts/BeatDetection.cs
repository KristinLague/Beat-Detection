using UnityEngine;

public class BeatDetection : MonoBehaviour 
{

	public OnBeatEvent onBeat;

	public int bufferSize = 1024;
	private int samplingRate = 44100;

	public bool limitBeats = false;
    public int limitAmount;
    public float changeThreshold = 0.1f;

	private const int bands = 12;
	private const int maximumLag = 100;
	private const float smoothDecay = 0.997f;
  
	private int framesSinceBeat = 0;
	private float framePeriod;

	private int ringBufferSize = 120;
	private int currentRingBufferPosition = 0;

	private float[] spectrum;
	private float[] previousSpectrum;

	private float[] averagesPerBand;
	private float[] onsets;
	private float[] notations;
	
	private AudioData audioData;
	private AudioSource audioSource;
	
	private void Start ()
	{
		onsets = new float[ringBufferSize];
		notations = new float[ringBufferSize];
		spectrum = new float[bufferSize];
		averagesPerBand = new float[bands];

		audioSource = GetComponent<AudioSource> ();
		samplingRate = audioSource.clip.frequency;

		framePeriod = (float)bufferSize / (float)samplingRate;

		previousSpectrum = new float[bands];
		for (int i = 0; i < bands; i++)
			previousSpectrum [i] = 100.0f;

		audioData = new AudioData (maximumLag, smoothDecay, framePeriod, GetBandWidth () * 2);
	}

	void Update ()
	{
			audioSource.GetSpectrumData (spectrum, 0, FFTWindow.BlackmanHarris);
			CalculateAveragePerBand (spectrum);

			float onset = 0;
			for (int i = 0; i < bands; i++) {
				float spectrumValue = Mathf.Max (-100.0f, 20.0f * Mathf.Log10 (averagesPerBand [i]) + 160); 
				spectrumValue *= 0.025f;
				float dbIncrement = spectrumValue - previousSpectrum [i]; 
				previousSpectrum [i] = spectrumValue; 
				onset += dbIncrement; 
			}

			onsets [currentRingBufferPosition] = onset;

			audioData.UpdatedAudioData (onset);
			

		float maximumDelay = 0.0f;
		int tempo = 0;
			
		for (int i = 0; i < maximumLag; i++) 
		{
			float delayValue = Mathf.Sqrt (audioData.DelayAtIndex (i));
			if (delayValue > maximumDelay) 
			{
				maximumDelay = delayValue;
				tempo = i;
			}
		}

		float maximumNotation = -999999;
		int maximumNotationIndex = 0;
		
		for (int i = tempo / 2; i < Mathf.Min (ringBufferSize, 2 * tempo); i++) 
		{
			float notation = onset +notations [(currentRingBufferPosition - i + ringBufferSize) % ringBufferSize] - 
			(changeThreshold * 100f) * Mathf.Pow (Mathf.Log ((float)i / (float)tempo), 2);
				
			if (notation > maximumNotation) {
				maximumNotation = notation;
				maximumNotationIndex = i;
			}
		}

		notations [currentRingBufferPosition] = maximumNotation;
			
		float minimumNotation = notations [0];
		for (int i = 0; i < ringBufferSize; i++)
		{
			if (notations [i] < minimumNotation)
			{
				minimumNotation =notations [i];
			}		
		}
				
		for (int i = 0; i < ringBufferSize; i++)
		{
			notations [i] -= minimumNotation;
		}
				
		maximumNotation = notations [0];
		maximumNotationIndex = 0;
		for (int i = 0; i < ringBufferSize; i++) {
			if (notations [i] > maximumNotation) {
				maximumNotation = notations [i];
				maximumNotationIndex = i;
			}
		}

		framesSinceBeat++;

		if (maximumNotationIndex == currentRingBufferPosition) 
		{
            if (limitBeats)
            {
                if (framesSinceBeat > tempo / limitAmount)
                {
                    onBeat.Invoke();   
                    framesSinceBeat = 0;
                }
            } 
			else
			{
                onBeat.Invoke();
            }
        }

        currentRingBufferPosition++;
        if (currentRingBufferPosition >= ringBufferSize)
		{
			currentRingBufferPosition = 0;
		}
			
	}

	public float GetBandWidth ()
	{
		return (2f / (float)bufferSize) * (samplingRate / 2f) * .5f;
	}

	public int FrequenceByIndex (int frequencyIndex)
	{
		if (frequencyIndex < GetBandWidth ())
		{
			return 0;
		}
		
		if (frequencyIndex > samplingRate / 2 - GetBandWidth ())
		{
			return (bufferSize / 2);
		}
	
		float fraction = (float)frequencyIndex / (float)samplingRate;
		return Mathf.RoundToInt (bufferSize * fraction);
	
	}

	public void CalculateAveragePerBand (float[] spectrumData)
	{
		for (int i = 0; i < bands; i++) 
		{
			float averagePower = 0;

			int lowFrequencyIndex = (i == 0) ? 0 : (int)((samplingRate / 2) / Mathf.Pow (2, bands - i));
			int highFrequencyIndex = (int)((samplingRate / 2) / Mathf.Pow (2, bands - 1 - i));

			int lowBound = FrequenceByIndex (lowFrequencyIndex);
			int hiBound = FrequenceByIndex (highFrequencyIndex);

			for (int j = lowBound; j <= hiBound; j++) {
				
				averagePower += spectrumData [j];
			}
			
			averagePower /= (hiBound - lowBound + 1);
			averagesPerBand [i] = averagePower;
		}
	}

	private class AudioData
	{
		private int index;
		private int delayLength;
		private float smoothDecay;
		private float octaveWidth;
        private float framePeriod;

        private float[] delays;
		private float[] outputs;
		private float[] bpms;
		private float[] weights;

		public AudioData (int delayLength, float smoothDecay, float framePeriod, float bandwidth)
		{
			index = 0;

			this.octaveWidth = bandwidth;
			this.smoothDecay = smoothDecay;
			this.delayLength = delayLength;
            this.framePeriod = framePeriod;

            delays = new float[delayLength];
			outputs = new float[delayLength];
			bpms = new float[delayLength];
			weights = new float[delayLength];

            ApplyWeights();
        }

		public void ApplyWeights()
		{
			for (int i = 0; i < delayLength; i++) 
			{
				bpms [i] = 60.0f / (framePeriod * i);
				weights [i] = Mathf.Exp (-0.5f * Mathf.Pow (Mathf.Log (bpms [i] / 120f) / Mathf.Log (2.0f) / octaveWidth, 2.0f));
			}
		}

		public void UpdatedAudioData (float onset)
		{
			delays [index] = onset;

			for (int i = 0; i < delayLength; i++) 
			{
				int delayIndex = (index - i + delayLength) % delayLength;
				outputs [i] += (1 - smoothDecay) * (delays [index] * delays [delayIndex] - outputs [i]);
			}

            index++;
            if (index >= delayLength)
				index = 0;
		}

		public float DelayAtIndex (int delayIndex)
		{
			return weights [delayIndex] * outputs [delayIndex];
		}
	}
}
