pdf:
	lualatex --shell-escape --interaction=nonstopmode thesis.tex
	biber thesis
	lualatex --shell-escape --interaction=nonstopmode thesis.tex
	lualatex --shell-escape --interaction=nonstopmode thesis.tex

clean:
	rm -f thesis.aux thesis.bbl thesis.bcf thesis.blg thesis.log thesis.pdf thesis.run.xml thesis.toc thesis.out
