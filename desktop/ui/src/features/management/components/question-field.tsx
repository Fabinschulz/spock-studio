import { Button, TextField } from '@mariliz/ui-kit';
import { useRef } from 'react';

import { MAX_QUESTION, MIN_QUESTION, SUGGESTED_QUESTIONS } from '../domain/assistant';
import askStyles from './ask.module.scss';

interface QuestionFieldProps {
  label: string;
  question: string;
  disabled: boolean;
  onChange: (question: string) => void;
}

/**
 * As sugestões vêm antes do campo e só o preenchem, levando o foco a ele: perguntar envia dados ao provedor, e isso
 * continua sendo um clique da pessoa.
 */
export function QuestionField({ label, question, disabled, onChange }: QuestionFieldProps) {
  const field = useRef<HTMLTextAreaElement>(null);

  const suggest = (text: string) => {
    onChange(text);
    field.current?.focus();
  };

  return (
    <>
      <div className={askStyles.suggestions} role="group" aria-label="Sugestões de pergunta">
        {SUGGESTED_QUESTIONS.map((text) => (
          <Button
            key={text}
            type="button"
            variant="outlined"
            size="small"
            disabled={disabled}
            onClick={() => suggest(text)}
          >
            {text}
          </Button>
        ))}
      </div>
      <TextField
        label={label}
        multiline
        minRows={2}
        fullWidth
        value={question}
        disabled={disabled}
        inputRef={field}
        onChange={(event) => onChange(event.target.value)}
        helperText={`Entre ${MIN_QUESTION} e ${MAX_QUESTION} caracteres.`}
      />
    </>
  );
}
