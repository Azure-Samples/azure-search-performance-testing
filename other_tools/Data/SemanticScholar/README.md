# Semantic Scholar Data Upload for Azure Cognitive Search

This application contains information on how to create an Azure Cognitive Search Index with data provided by [Semantic Scholar](http://s2-public-api-prod.us-west-2.elasticbeanstalk.com/corpus/download/).

For uploading the data, we will use the Azure Cognitive Search SDK because it allows for retries and exponential backoff. 
The average document size is ~3 KB Each corpus file (e.g. s2-corpus-000.gz) has ~998,537 documents and is ~1,680MB in raw text (1.68KB / DOC)
