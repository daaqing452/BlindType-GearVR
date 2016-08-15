import numpy as np
import os
import sys


N_SEGMENT_MAX = 5
T_REMOVE = 1.5


def Print():
	global N_SEGMENT_MAX
	global name
	global algorithm
	global phase
	global n_segment
	global n_word
	global n_word_corrected
	global n_word_uncorrected
	global n_word_unpredicted
	global n_word_cannot_be_typed
	global n_rank
	global t_drag
	global t_need
	
	s = name
	s += ',' + algorithm
	s += ',' + str(phase * N_SEGMENT_MAX + n_segment)
	s += ',' + str(n_sentence)
	
	wpm = n_word / ((t_need + t_drag) / 1000.0 / 60.0)
	s += ',' + str(wpm)
	
	for i in range(1, 25):
		n_rank[i] += n_rank[i - 1]
	top_1 = n_rank[0] / n_word
	top_5 = n_rank[4] / n_word
	top_25 = n_rank[24] / n_word
	s += ',' + str(top_1) + ',' + str(top_5) + ',' + str(top_25)
	
	corrected_error = 1.0 * n_word_corrected / n_word
	uncorrected_error = 1.0 * n_word_uncorrected / n_word
	unpredicted_error = 1.0 * n_word_unpredicted / n_word_typed
	cannot_be_typed = 1.0 * n_word_cannot_be_typed / n_word
	s += ',' + str(corrected_error) + ',' + str(uncorrected_error) + ',' + str(unpredicted_error) + ',' + str(cannot_be_typed)
	
	drag_time = 1.0 * t_drag / (t_need + t_drag)
	s += ',' + str(drag_time)
	
	# print(s)
	global fo
	fo.write(s + '\n')


def Init_Segment():
	global n_sentence
	global n_word
	global n_word_corrected
	global n_word_uncorrected
	global n_word_unpredicted
	global n_word_cannot_be_typed
	global n_word_typed
	global n_rank
	global t_sentence_switch
	global t_drag
	global t_need
	
	n_sentence = 0
	n_word = 0
	n_word_corrected = 0
	n_word_uncorrected = 0
	n_word_unpredicted = 0
	n_word_cannot_be_typed = 0
	n_word_typed = 0
	n_rank = np.zeros((25), dtype=int)
	t_sentence_switch = 0
	t_drag = 0
	t_need = 0

def Init_Sentence():
	global samples
	global words
	global ranks
	global correcteds
	global occurs
	
	clicks = 0
	samples = []
	words = []
	ranks = np.zeros((9), dtype=int)
	correcteds = np.zeros((9), dtype=int)
	occurs = np.zeros((9), dtype=int)

def Init_Word():
	global clicks
	clicks = 0


print('args:', sys.argv)
names = ['csy', 'cyz', 'dwz', 'gyz', 'hzs', 'jwf', 'lhy', 'lyq', 'syq', 'wq', 'xwj', 'ydl', 'yh', 'yy', 'yzc', 'zls', 'ztc']
algorithms = ['Absolute', 'Relative']
phases = [0, 1]
status = 'nothing'

fo = open('a.csv', 'w')
fo.write('name,algorithm,segment,n_word,wpm,top_1,top_5,top_25,corrected_error,uncorrected_error,unpredicted_error,cannot_be_typed,drag_time\n')

for name in names:
	for algorithm in algorithms:
		for phase in phases:
			file_name = name + '/log-' + name + '-' + algorithm + '-' + str(phase) + '.txt'
			fi = open(file_name, 'r')
			
			lines = []
			n_sentence_left = 0
			while True:
				line = fi.readline()
				if len(line) == 0:
					break
				if ord(line[-1]) == 10:
					line = line[:-1]
				lines.append(line)
				arr = line.split(' ')
				op = arr[1]
				if op == 'sentence':
					n_sentence_left += 1
			lines.append('0 sentence 0 0')
			fi.close()
			
			n_segment = 0
			t_pre = -1
			draging = False
			Init_Segment()
			Init_Sentence()
			Init_Word()
			
			for line in lines:
				arr = line.split(' ')
				t = int(arr[0])
				op = arr[1]
				
				if op == 'sentence':
					n_sentence += 1
					n_word += len(samples)
					for i in range(len(samples)):
						sample = samples[i]
						
						if correcteds[i] > 0:
							n_word_corrected += 1
						
						if sample != words[i]:
							n_word_uncorrected += 1
						else:
							n_rank[ranks[i]] += 1
						
						if occurs[i] == 0:
							n_word_cannot_be_typed += 1
					
					if (n_sentence > 1 and n_sentence * (N_SEGMENT_MAX - n_segment) > n_sentence_left):
						n_segment += 1
						n_sentence_left -= n_sentence
						Print()
						Init_Segment()
					
					Init_Sentence()
					samples = arr[3:]
				
				if op == 'click':
					clicks += 1
				
				if op == 'leftslip':
					if clicks > 0:
						clicks -= 1
					elif len(words) > 0:
						correcteds[len(words) - 1] += 1
						words = words[:-1]
				
				if op == 'dragbegin':
					t_drag_start = t
					draging = True
				
				if op == 'dragend':
					t_drag += t - t_drag_start
					draging = False
				
				if op == 'select':
					s = arr[2]
					rank = int(arr[3])
					exist = arr[4]
					n_word_typed += 1
					words.append(s)
					ranks[len(words) - 1] = rank
					if exist == 'False':
						n_word_unpredicted += 1
					else:
						occurs[len(words) - 1] = 1
					Init_Word()

				if t - t_pre < T_REMOVE * 1000 and draging != True and op != 'sentence':
					t_need += t - t_pre
				t_pre = t
				
fo.close()