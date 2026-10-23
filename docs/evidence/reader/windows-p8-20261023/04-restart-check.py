a=snapshot("06-restart-verified")
assert len(a["EbookReadingSession"])==1 and len(a["Quote"])==0
assert a["AiGenerationBatch"][0]["Status"]==1
shot("06-restarted")
close()
