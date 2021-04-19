# General tips on .jmx config file

## ThreadGroup or any other plugin

This section defines the test strategy, ie "ThreadGroup.on_sample_error" if the test should stop once it encounters an error. "TargetLevel" is the final number of concurrent calls that the service will receive after a period ("RampUp") stepped into a number of steps 

```xml
        <stringProp name="ThreadGroup.on_sample_error">continue</stringProp>
        <stringProp name="TargetLevel">100</stringProp>
        <stringProp name="RampUp">1</stringProp>
        <stringProp name="Steps">5</stringProp>
        <stringProp name="Hold"></stringProp>
```

## HTTPSamplerProxy

This section includes the parameters and the body of your REST API call, must adhere to [the expected Azure Cognitive Search syntax](https://docs.microsoft.com/en-us/azure/search/query-lucene-syntax). You can set the search instance, the index name, the api-version and the "Argument.value" itself includes the search body (in this case a random term from a defined variable, that reads from a CSV list)

```xml
          <elementProp name="HTTPsampler.Arguments" elementType="Arguments">
            <collectionProp name="Arguments.arguments">
              <elementProp name="" elementType="HTTPArgument">
                <boolProp name="HTTPArgument.always_encode">false</boolProp>
                <stringProp name="Argument.value">{  &#xd;
     &quot;search&quot;: &quot;${randomsearchterm}&quot;,  &#xd;
     &quot;skip&quot;:0,&#xd;
     &quot;top&quot;: 5,&#xd;
     &quot;queryType&quot;: &quot;full&quot;&#xd;
}</stringProp>
                <stringProp name="Argument.metadata">=</stringProp>
              </elementProp>
            </collectionProp>
          </elementProp>
          <stringProp name="HTTPSampler.domain">your_instance.search.windows.net</stringProp>
          <stringProp name="HTTPSampler.port">443</stringProp>
          <stringProp name="HTTPSampler.protocol">https</stringProp>
          <stringProp name="HTTPSampler.contentEncoding"></stringProp>
          <stringProp name="HTTPSampler.path">/indexes/your_index_name/docs/search?api-version=2020-06-30</stringProp>
```

The timeouts are optional, in this case set at 10 secs

```xml
          <stringProp name="HTTPSampler.connect_timeout">10000</stringProp>
          <stringProp name="HTTPSampler.response_timeout">10000</stringProp>
```

## HeaderManager

This section includes the header values needed for the REST API to go through the service. API_KEY will be substituted by a Devops step for the real key

```xml
              <stringProp name="Header.name">api-key</stringProp>
              <stringProp name="Header.value">API_KEY</stringProp>
            </elementProp>
            <elementProp name="" elementType="Header">
              <stringProp name="Header.name">Content-Type</stringProp>
              <stringProp name="Header.value">application/json</stringProp>
```

## CSVDataSet

The search engine has a cache, if you repeat the same query the latency seen in the results will not be realistic compared to scenario where your users query the system with diverse terms. This module defines the input list used to query starting from first line (if you need a random ordered term from the CSV use https://www.blazemeter.com/blog/introducing-the-random-csv-data-set-config-plugin-on-jmeter)

## Examples

### Example 1: Simple test scenario using "Thread Group"

[`sample.jmx`](./jmeter/sample.jmx)

### Example 2: Step growth scenario using "Concurrency Thread Group"

[`sample_steps.jmx`](./jmeter/sample_steps.jmx)

More info on [Concurrency Thread Group Plugin](https://jmeter-plugins.org/wiki/ConcurrencyThreadGroup/)
