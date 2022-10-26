using UnityEngine;

public enum Visualization
{
    LINE,
    RING,
    RINGWITHCENTER,
    DOUBLELINE,
	CIRCLE
}
public class AudioVisualization : MonoBehaviour 
{
    //Material for our LineRenderer
    public Material material;
    public Visualization visualizationMode;
    public Gradient optionA = new Gradient();
    public Gradient optionB = new Gradient();

    public Gradient colorGradient = new Gradient();

    //If we are displaying a Ring we need a radius
    public float minimumRadius;
    public float bufferAreaSize;
    public float lineMultiplier;
    public float smoothingSpeed;
    public float maximumScale;
    public int segments;
    public float sampledPercentage;

    //LineRenderer Prefab
    public GameObject lineRendererPrefab;

    //Calculated Values
    private float averagePower;
    private float db;
    private float pitch;

	private AudioSource audioSource;
    private float[] samples;
    private float[] spectrum;
    private float sampleRate;

    private float[] lineScales;
    private LineRenderer circleRenderer;
    private LineRenderer[] lines;
    private LineRenderer[] doubledLines;

    private Vector3[] circlePositions;

    private float currentRadius;
    private float currentColorValue;

    private void Awake()
	{
        //Getting our Audiosource and setting up our samples,spectrum and samplerate
        audioSource = GetComponent<AudioSource>();
        sampleRate = AudioSettings.outputSampleRate;

		//You should always set the length to 1024;
        samples = new float[1024];
        spectrum = new float[1024];

        //We need to instantiate differently depending on which 
        //visualization mode we choose
        switch(visualizationMode)
		{
			case Visualization.LINE:
                InitializeLine();
                break;

			case Visualization.RING:
                InitializeRing();
                break;

			case Visualization.RINGWITHCENTER:
                InitializeRing();
                break;

			case Visualization.DOUBLELINE:
                InitializeDoubleLine();
                break;

			case Visualization.CIRCLE:
                InitializeCircle();
                break;
        }
	}

	private void Update()
	{
        //We need to always keep analyzing our Music.
        AnalyzeAudio();
        CalculateLineScales();

        if(visualizationMode == Visualization.LINE)
		{
            UpdateLine();
        }
		else if (visualizationMode == Visualization.RING)
		{
            UpdateRing();
        } 
		else if (visualizationMode == Visualization.RINGWITHCENTER)
		{
            UpdateCenter();
            UpdateRing();
        }
		else if (visualizationMode == Visualization.DOUBLELINE)
		{
            UpdateDoubleLine();
        } 
		else if (visualizationMode == Visualization.CIRCLE)
		{
            UpdateCircle();
        }
	}

	private void InitializeLine()
	{
        //TODO CHANGE CAMERA SHIT
        Vector3 newCameraPos = Camera.main.transform.position;
        newCameraPos.x += 74f;
        newCameraPos.y += 20f;
        Camera.main.transform.position = newCameraPos;

        //First we need to initialize our Arrays, one for the scales
        //we are going to need to scale each line depending on the spectrum
        //value and the other one holds reference to all of our LineRenderers.
        lineScales = new float[segments];
		lines = new LineRenderer[lineScales.Length];

		//We are looping through our lineRenderers to instantiate
        //as many of them as we set as our segments.
        for (int i = 0; i < lines.Length; i++)
		{
            GameObject go = Instantiate(lineRendererPrefab);
            go.transform.position = Vector3.zero;

            LineRenderer line = go.GetComponent<LineRenderer>();
            line.sharedMaterial = material;

            line.positionCount = 2;
            line.useWorldSpace = true;
            lines[i] = line;
        }
    }

	private void InitializeDoubleLine()
	{
		//TODO CHANGE CAMERA SHIT
        Vector3 newCameraPos = Camera.main.transform.position;
        newCameraPos.x += 74f;
        newCameraPos.y -= 1f;
        Camera.main.transform.position = newCameraPos;

        //First we need to initialize our Arrays, one for the scales
        //we are going to need to scale each line depending on the spectrum
        //value and the other one holds reference to all of our LineRenderers.
        lineScales = new float[segments];
		lines = new LineRenderer[lineScales.Length];
        doubledLines = new LineRenderer[lineScales.Length];

        //We are looping through our lineRenderers to instantiate
        //as many of them as we set as our segments.
        for (int i = 0; i < lines.Length; i++)
		{
            GameObject go = Instantiate(lineRendererPrefab);
            GameObject doubledGo = Instantiate(lineRendererPrefab);

            go.transform.position = Vector3.zero;
            doubledGo.transform.position = Vector3.zero;

            LineRenderer line = go.GetComponent<LineRenderer>();
            line.sharedMaterial = material;

            LineRenderer doubledLine = doubledGo.GetComponent<LineRenderer>();
            doubledLine.sharedMaterial = material;

            line.positionCount = 2;
            line.useWorldSpace = true;

            doubledLine.positionCount = 2;
            doubledLine.useWorldSpace = true;

            lines[i] = line;
            doubledLines[i] = doubledLine;
        }	
	}

	private void InitializeRing()
	{
		//First we need to initialize our Arrays, one for the scales
		//we are going to need to scale each line depending on the spectrum
		//value and the other one holds reference to all of our LineRenderers.
        lineScales = new float[segments + 1];
        lines = new LineRenderer[lineScales.Length];

        //We are looping through our lineRenderers to instantiate
        //as many of them as we set as our segments amount plus one.
        for (int i = 0; i < lines.Length; i++)
		{
            GameObject go = Instantiate(lineRendererPrefab);
            go.transform.position = Vector3.zero;

            LineRenderer line = go.GetComponent<LineRenderer>();
            line.sharedMaterial = material;

            line.positionCount = 2;
            line.useWorldSpace = true;
            lines[i] = line;
        }

        currentRadius = minimumRadius;
    }

	private void InitializeCircle()
	{
        lineScales = new float[segments + 1];
        
		GameObject go = Instantiate(lineRendererPrefab);
        go.transform.position = Vector3.zero;

        circleRenderer = go.GetComponent<LineRenderer>();
        circleRenderer.sharedMaterial = material;

        circleRenderer.positionCount = segments + 1;
        circleRenderer.useWorldSpace = true;

        circleRenderer.startWidth = circleRenderer.endWidth = 0.5f;

        float x;
        float y;
        float z = 0f;
        circlePositions = new Vector3[segments + 1];
        float angle = 20f;

        for (int i = 0; i < (segments + 1); i++)
        {
            x = Mathf.Sin(Mathf.Deg2Rad * angle) * minimumRadius;
            y = Mathf.Cos(Mathf.Deg2Rad * angle) * minimumRadius;

            circleRenderer.SetPosition(i, new Vector3(x, y, z));
            circlePositions[i] = new Vector3(x, y, z);
            angle += (360f / segments);
        }

        circleRenderer.colorGradient = colorGradient;
    }

	private void UpdateLine()
	{
        for (int i = 0; i < lines.Length; i++)
		{
            lines[i].SetPosition(0, Vector3.right * i);
            lines[i].SetPosition(1, (Vector3.right * i) + Vector3.up * (bufferAreaSize + lineScales[i]));

			lines[i].startWidth = 1f;
            lines[i].endWidth = 1f;

			//Changing the color of the Material depending on the linescale
            lines[i].startColor = colorGradient.Evaluate(0);
            lines[i].endColor = colorGradient.Evaluate((lineScales[i] - 1) / (maximumScale - 1f));
        }

    }

	private void UpdateDoubleLine()
	{
		for (int i = 0; i < lines.Length; i++)
		{
            lines[i].SetPosition(0, Vector3.right * i);
            lines[i].SetPosition(1, (Vector3.right * i) + Vector3.up * (bufferAreaSize + lineScales[i]));

			doubledLines[i].SetPosition(0, Vector3.right * i);
			doubledLines[i].SetPosition(1, (Vector3.right * i) + Vector3.down * (bufferAreaSize + lineScales[i]));

			lines[i].startWidth = doubledLines[i].startWidth  = 3f;
            lines[i].endWidth = doubledLines[i].endWidth = 3f;

            lines[i].startColor = doubledLines[i].startColor = colorGradient.Evaluate(0);
            lines[i].endColor = doubledLines[i].endColor = colorGradient.Evaluate((lineScales[i] - 1) / (maximumScale - 1f));

        }
	}

	private void UpdateRing()
	{

        for (int i = 0; i < lines.Length; i++)
		{
            float t = i / (lines.Length - 2f);
            float a = t * Mathf.PI * 2f;

            Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            //float currentRadius = minimumRadius;
            float maxRadius = (currentRadius + bufferAreaSize + lineScales[i]);

            lines[i].SetPosition(0, direction * currentRadius);
            lines[i].SetPosition(1, direction * maxRadius);

			//Calculating the spacing between two lines to avoid a weird shape
            lines[i].startWidth = Spacing(currentRadius);
            lines[i].endWidth = Spacing(maxRadius);

            //Changing the color of the Material depending on the linescale
            lines[i].startColor = colorGradient.Evaluate(0);
            lines[i].endColor = colorGradient.Evaluate((lineScales[i] - 1) / (maximumScale - 1f));
        }
    }

	private void UpdateCircle()
	{
        for (int i = 0; i < circlePositions.Length; i++)
		{
			float t = i / (segments - 2f);
            float a = t * Mathf.PI * 2f;

            Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

            float maxRadius = (minimumRadius + bufferAreaSize + lineScales[i]);

            Vector3 changedY = direction * maxRadius;

			if(i == circlePositions.Length - 1)
			{
				circleRenderer.SetPosition(i, circlePositions[0]);
			} 
			else 
			{
				circleRenderer.SetPosition(i, changedY);
			}

			circlePositions[i] = circleRenderer.GetPosition(i);

        }
    	//material.SetFloat("a", maximumScale);
		//material.SetFloat("b", minimumRadius);
    }

	public void AddRadiusWidth()
	{
        if(colorGradient == optionA)
       {
            colorGradient = optionB;
        } else
        {
            colorGradient = optionA;
        } 
    }

	public void UpdateCenter()
	{
         
    }

	public void ChangeColorOnBeat()
	{
        currentColorValue += .1f;

		if(currentColorValue > 1)
		{
            currentColorValue = 0;
        }
    }

	private float Spacing(float radius) 
	{
        float c = 2 * Mathf.PI * radius;
        float n = lines.Length;
        return c / n;
    }

	private void AnalyzeAudio()
	{
        audioSource.GetOutputData(samples, 0);

        //Getting the average power by getting the sum of all the squared samples
        float sum = 0;

        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }

        averagePower = Mathf.Sqrt(sum / samples.Length);

        //Getting the DB Value
        db = 20 * Mathf.Log10(averagePower * 0.1f);

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        //Getting the pitch
        float maxV = 0;
        int maxN = 0;

        for (int i = 0; i < samples.Length; i++)
        {
            if (!(spectrum[i] > maxV) || !(spectrum[i] > .0f))
            {
                continue;
            }

            maxV = spectrum[i];
            maxN = i;
        }

        float frequenceN = maxN;
        if (maxN > 0 && maxN < samples.Length - 1)
        {
            float dl = spectrum[maxN - 1] / spectrum[maxN];
            float dr = spectrum[maxN + 1] / spectrum[maxN];
            frequenceN += .5f * (dr * dr - dl * dl);
        }

        pitch = frequenceN * (sampleRate / 2) / samples.Length;
    }

	private void CalculateLineScales()
	{
        int index = 0;
        int spectralIndex = 0;
        //int averageSize = 1;
        int averageSize =(int) Mathf.Abs(samples.Length * sampledPercentage);
        averageSize /= segments;
        if(averageSize < 1)
        {
            averageSize = 1;
        }
        //Debug.LogError(averageSize);

        while(index < segments)
		{
            int i = 0;
            float sum = 0;
			while(i < averageSize)
			{
                sum += spectrum[spectralIndex];
                spectralIndex++;
                i++;
            }

			float yScale = sum / averageSize * lineMultiplier;
            lineScales[index] -= Time.deltaTime * smoothingSpeed;

			if(lineScales[index] < yScale)
			{
                lineScales[index] = yScale;
            }

			if(lineScales[index] > maximumScale)
			{
                lineScales[index] = maximumScale;
            }
            index++;
        }
    }
	
}
