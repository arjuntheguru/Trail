import React from 'react';
import { Modal } from 'react-bootstrap';
import TaskForm from './TaskForm';

const AddTask = ({ show, onClose, skip }) => {
    
    return (
        <Modal show={show} onHide={onClose} centered size="lg" >          
            <Modal.Body className="bgGradient">{<TaskForm onClose={onClose} skip={skip}/>}</Modal.Body>
        </Modal>
    )
};

export default AddTask;